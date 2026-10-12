using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Models;

namespace ProcureAI.Ai.Providers;

public sealed class DemoAgentProvider
{
    public DomainStepResult Execute(DomainStepRequest request)
    {
        object output = request.Step switch
        {
            "needs" => request.Input.Criteria,
            "suppliers" => ExtractOffers(request.TargetId, request.Input.Sources),
            "compare" => request.Input.Scores,
            "recommend" => Recommend(request.Input),
            "chat" => new ReasonDto("Mod demonstrativ: fragment relevant din sursele încărcate: " +
                request.Input.Sources[0].Text[..Math.Min(500, request.Input.Sources[0].Text.Length)], [request.Input.Sources[0].SourceId]),
            _ => throw new AppException("unsupported_agent_role")
        };
        return new DomainStepResult("completed", JsonDefaults.Serialize(output), "demo-fixtures-no-model", 0, 0, 0);
    }

    private static SuppliersResultDto ExtractOffers(Guid runId, IReadOnlyList<SourceDto> sources)
    {
        var offers = new List<OfferDto>();
        var unknowns = new List<string>();
        foreach (var source in sources)
        {
            try
            {
                using var json = JsonDocument.Parse(source.Text);
                var root = json.RootElement;
                string? Text(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                decimal? Number(string name) => root.TryGetProperty(name, out var v) && v.TryGetDecimal(out var d) ? d : null;
                int? Days() => root.TryGetProperty("leadDays", out var v) && v.TryGetInt32(out var d) ? d : null;
                bool? Meets() => root.TryGetProperty("meetsRequirements", out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
                var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(runId.ToString() + source.DocumentId))[..16]);
                offers.Add(new OfferDto(id, source.DocumentId, Text("supplierLabel") ?? source.FileName,
                    Text("currency"), Number("quantity"), Text("unit"), Number("net"), Number("shipping"), Days(), Meets(), [source.SourceId]));
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            { unknowns.Add($"{source.FileName}: demo mode requires a structured JSON offer; use Foundry for other text."); }
        }
        return new SuppliersResultDto(offers, unknowns);
    }

    private static RecommendationDto Recommend(AgentInputDto input)
    {
        if (input.Scores.Count == 0) throw new AppException("offers_required", 422);
        var winner = input.Offers.Single(x => x.Id == input.Scores[0].Id);
        return new RecommendationDto(winner.Id, input.SnapshotHash,
            [new ReasonDto($"Mod demonstrativ: {winner.SupplierLabel} are scorul maxim {input.Scores[0].Score:F4}, cost net cu transport {input.Scores[0].Cost:F2} {winner.Currency} și livrare în {winner.LeadDays} zile.", winner.SourceIds)], []);
    }
}
