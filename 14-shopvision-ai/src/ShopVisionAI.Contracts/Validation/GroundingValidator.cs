using ShopVisionAI.Contracts.Dto;

namespace ShopVisionAI.Contracts.Validation;

public static class GroundingValidator
{
    public static void Validate(GroundedResponse response, IReadOnlyList<Candidate> candidates)
    {
        var allowed = candidates.SelectMany(c => c.SourceIds).ToHashSet(StringComparer.Ordinal);
        if (
            response.Answer.Length > 6000
            || response.InputTokens < 0
            || response.OutputTokens < 0
            || response.InputTokens + response.OutputTokens > 15000
            || response.SourceIds.Any(x => !allowed.Contains(x))
            || (candidates.Count > 0 && response.SourceIds.Count == 0)
        )
            throw new ArgumentException("invalid_grounded_response");
        if (
            response.Explanations.Count != candidates.Count
            || response.Explanations.Select(x => x.Sku).Distinct().Count() != candidates.Count
        )
            throw new ArgumentException("invalid_candidate_explanations");
        foreach (var e in response.Explanations)
        {
            var c =
                candidates.SingleOrDefault(c => c.Sku == e.Sku)
                ?? throw new ArgumentException("invented_candidate");
            if (
                string.IsNullOrWhiteSpace(e.Reason)
                || e.Reason.Length > 2000
                || e.SourceIds.Count == 0
                || e.SourceIds.Any(s => !c.SourceIds.Contains(s))
            )
                throw new ArgumentException("invalid_candidate_sources");
        }
    }
}
