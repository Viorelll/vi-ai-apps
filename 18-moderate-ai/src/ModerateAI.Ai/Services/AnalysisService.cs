using System.Text.Json;
using Microsoft.Extensions.Options;
using ModerateAI.Ai.Options;
using ModerateAI.Ai.Providers;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Models;
using ModerateAI.Domain.Services;

namespace ModerateAI.Ai.Services;

public sealed class AnalysisService(
    ContentSafetyAdapter safety,
    FoundryExplainer explainer,
    IOptions<ProviderOptions> options
)
{
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken ct)
    {
        if (
            request.OperationId == Guid.Empty
            || (string.IsNullOrWhiteSpace(request.Text) && request.ImageObjectKey == null)
            || request.Text?.EnumerateRunes().Count() > 10000
        )
            throw new DomainException(400, "invalid_analysis_input");
        if (options.Value.Mode == "Demo")
        {
            // Explicit artificial fixtures, not a safety classifier. No arbitrary text is deemed safe.
            int? severity =
                request.ImageObjectKey == null
                    ? request.Text switch
                    {
                        "[demo:allow]" => 0,
                        "[demo:review]" => 2,
                        "[demo:block]" => 4,
                        _ => null,
                    }
                    : null;
            var scores = PolicyEvaluator
                .Categories.Select(c => new SafetyScore(c, c == "Violence" ? severity ?? 0 : 0))
                .ToArray();
            var raw = JsonSerializer.Serialize(
                new
                {
                    demo = true,
                    fixture = severity,
                    categoriesAnalysis = scores.Select(s => new
                    {
                        category = s.Category,
                        severity = s.Severity,
                    }),
                }
            );
            var snapshot = new SafetySnapshot(
                "Demo (synthetic, no AI)",
                SafetyResponseMapper.ApiVersion,
                "FourSeverityLevels",
                scores,
                severity != null,
                raw
            );
            var policy = PolicyEvaluator.Evaluate(snapshot);
            return new(
                snapshot,
                "DEMO: rezultat artificial. " + string.Join(" ", policy.Reasons),
                0,
                0
            );
        }
        if (options.Value.PromptShields && !string.IsNullOrWhiteSpace(request.Text))
            await safety.ShieldAsync(request.Text, ct);
        var actual = await safety.AnalyzeAsync(request, ct);
        var decision = PolicyEvaluator.Evaluate(actual);
        if (!actual.Complete)
            return new(
                actual,
                "Analiza furnizorului este incompletă; este necesară verificarea locală.",
                0,
                0
            );
        var explanation = await explainer.ExplainAsync(actual, decision, ct);
        return new(
            actual,
            explanation.Explanation,
            explanation.InputTokens,
            explanation.OutputTokens
        );
    }
}
