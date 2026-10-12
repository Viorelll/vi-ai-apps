using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Entities;
using ProcureAI.Domain.Models;

namespace ProcureAI.Application.Services;

public sealed class WorkflowProcessor(IProcurementRepository repository, IAiGateway ai, OfferScorer scorer, EvidenceValidator evidence)
{
    private static readonly string[] Roles = ["needs", "suppliers", "compare", "recommend"];

    public async Task ProcessAsync(DurableJob job, CancellationToken ct)
    {
        var snapshot = await repository.SnapshotAsync(job.RunId!.Value, ct);
        if (snapshot.Run.Stale) throw new AppException("sources_changed", 422);
        var role = Roles.FirstOrDefault(role => !snapshot.Steps.Any(step => step.Role == role));
        if (role is null) return;
        var criteria = JsonDefaults.Deserialize<CriteriaDto>(snapshot.Run.CriteriaJson);
        var scores = snapshot.Scores;
        if (role is "compare" or "recommend")
        {
            try { scores = scorer.Score(snapshot.Offers, criteria); }
            catch (AppException exception)
            {
                await repository.StopRunAsync(job, "NeedsInput", exception.Code, ct);
                return;
            }
        }
        var sources = snapshot.Sources;
        if (role == "suppliers") sources = sources.Where(x => snapshot.OfferDocumentIds.Contains(x.DocumentId)).ToArray();
        var input = new AgentInputDto(snapshot.Run.Brief, criteria, sources, snapshot.Offers, scores, snapshot.Run.SnapshotHash);
        var inputJson = JsonDefaults.Serialize(input);
        if (inputJson.Length > 45000) throw new AppException("context_limit", 422);
        var remaining = snapshot.Run.BudgetTokens - snapshot.Run.UsedTokens;
        var outputLimit = (int)Math.Min(1200, remaining - inputJson.Length / 2 - 1500);
        if (outputLimit < 100) throw new AppException("token_budget_exceeded", 422);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inputJson)));
        var result = await ai.StepAsync(new DomainStepRequest(Guid.NewGuid(), job.Id, "ProcureAI", snapshot.Run.Id,
            role, hash, "procure-v1", input, sources.Select(s => s.DocumentId).Distinct().ToArray(), outputLimit), ct);
        if (result.InputTokens < 0 || result.OutputTokens < 0) throw new AppException("invalid_usage", 422);
        var step = new AgentStep
        {
            RunId = snapshot.Run.Id,
            Role = role,
            InputHash = hash,
            OutputJson = result.OutputJson,
            Deployment = result.Deployment,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            DurationMs = result.DurationMs,
            Attempt = job.Attempt + 1
        };
        IReadOnlyList<OfferDto>? offers = null;
        RecommendationDto? recommendation = null;
        var state = role switch { "needs" => "Planning", "suppliers" => "Evaluating", "compare" => "Recommending", _ => "Completed" };
        if (result.Status == "needs_input") { state = "NeedsInput"; step.State = "NeedsInput"; }
        else if (result.Status != "completed") throw new AppException("invalid_agent_status", 422);
        else
        {
            try
            {
                using var output = JsonDocument.Parse(result.OutputJson);
                if (role == "needs")
                {
                    var received = JsonDefaults.Deserialize<CriteriaDto>(result.OutputJson);
                    if (received != criteria) throw new AppException("agent_changed_criteria", 422);
                }
                if (role == "suppliers")
                {
                    var parsed = JsonDefaults.Deserialize<SuppliersResultDto>(result.OutputJson);
                    offers = parsed.Offers.Select(offer => offer with { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Run.Id.ToString() + offer.DocumentId))[..16]) }).ToArray();
                    evidence.ValidateOffers(offers, sources, snapshot.OfferDocumentIds);
                    if (parsed.Unknowns.Count > 0) state = "NeedsInput";
                }
                if (role == "compare")
                {
                    var received = JsonDefaults.Deserialize<IReadOnlyList<OfferScoreDto>>(result.OutputJson);
                    if (!received.SequenceEqual(scores)) throw new AppException("agent_changed_scores", 422);
                }
                if (role == "recommend")
                {
                    recommendation = JsonDefaults.Deserialize<RecommendationDto>(result.OutputJson);
                    evidence.ValidateRecommendation(recommendation, scores, sources, snapshot.Run.SnapshotHash);
                }
            }
            catch (JsonException) { throw new AppException("invalid_agent_json", 422); }
        }
        if (snapshot.Run.UsedTokens + result.InputTokens + result.OutputTokens > snapshot.Run.BudgetTokens)
        {
            state = "Failed";
            step.State = "BudgetExceeded";
            recommendation = null;
        }
        await repository.CommitStepAsync(job, step, offers, recommendation, scores, state, ct);
    }
}
