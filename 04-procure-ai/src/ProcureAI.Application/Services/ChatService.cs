using System.Security.Cryptography;
using System.Text;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Models;

namespace ProcureAI.Application.Services;

public sealed class ChatService(IProcurementRepository repository, IAiGateway ai)
{
    public async Task<ChatResultDto> AskAsync(ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 4000)
            throw new AppException("invalid_question");
        var run = await repository.GetRunAsync(request.RunId, ct) ?? throw new AppException("run_not_found", 404);
        if (run.Stale) throw new AppException("sources_changed", 409);
        var terms = request.Question.ToLowerInvariant().Split([' ', '?', '.', ','], StringSplitOptions.RemoveEmptyEntries);
        var sources = run.Sources.Where(s => terms.Any(t => t.Length > 2 && s.Text.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Take(4).Select(s => s with { Text = s.Text[..Math.Min(6000, s.Text.Length)] }).ToArray();
        DomainStepResult result;
        string answer;
        if (sources.Length == 0)
        {
            answer = "Nu am găsit dovezi relevante în sursele acestei analize.";
            result = new DomainStepResult("completed", "{}", "no-evidence", 0, 0, 0);
        }
        else
        {
            var input = new AgentInputDto(run.Brief, run.Criteria, sources, run.Offers, run.Scores, run.SnapshotHash, request.Question);
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonDefaults.Serialize(input))));
            result = await ai.StepAsync(new DomainStepRequest(Guid.NewGuid(), Guid.NewGuid(), "ProcureAI", run.Id,
                "chat", hash, "procure-v1", input, sources.Select(s => s.DocumentId).Distinct().ToArray()), ct);
            var parsed = JsonDefaults.Deserialize<ReasonDto>(result.OutputJson);
            if (parsed.SourceIds.Count == 0 || parsed.SourceIds.Any(id => !sources.Any(s => s.SourceId == id)))
                throw new AppException("invalid_citations", 422);
            answer = parsed.Text;
            sources = sources.Where(s => parsed.SourceIds.Contains(s.SourceId)).ToArray();
        }
        var conversation = await repository.SaveChatAsync(request, answer, sources, result, ct);
        return new ChatResultDto(conversation, answer, sources.Select(s => s with { Text = s.Text[..Math.Min(500, s.Text.Length)] }).ToArray(),
            result.InputTokens, result.OutputTokens, result.DurationMs);
    }
}
