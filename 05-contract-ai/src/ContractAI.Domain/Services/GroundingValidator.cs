using System.Text.RegularExpressions;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Models;

namespace ContractAI.Domain.Services;

public static class GroundingValidator
{
    public static void Validate(
        Evidence[] evidence,
        IReadOnlyList<ClauseSource> sources,
        bool required = true
    )
    {
        if (evidence is null || (required && evidence.Length == 0) || evidence.Length > 32)
            throw new AppException(502, "invalid_evidence", "AI output is missing valid evidence.");
        foreach (var item in evidence)
        {
            var source = sources.FirstOrDefault(x => x.Id == item.ClauseId);
            if (
                source is null
                || string.IsNullOrWhiteSpace(item.Quote)
                || !source.Text.Contains(item.Quote, StringComparison.Ordinal)
            )
                throw new AppException(
                    502,
                    "invalid_evidence",
                    "AI cited a foreign source or an inexact quotation."
                );
        }
    }

    public static DateOnly? ResolveDueDate(int? days, DateOnly? verifiedAnchor) =>
        days is null or < 0 or > 36500 || verifiedAnchor is null
            ? null
            : verifiedAnchor.Value.AddDays(days.Value);

    public static void ValidateAnalysis(AnalysisResult result, ClauseSource[] sources)
    {
        if (
            string.IsNullOrWhiteSpace(result.Summary)
            || result.Summary.Length > 8000
            || result.Obligations is null
            || result.Findings is null
            || result.Unknowns is null
            || result.Obligations.Length > 100
            || result.Findings.Length > 100
        )
            throw new AppException(
                502,
                "invalid_analysis",
                "AI returned an invalid analysis schema."
            );
        foreach (var item in result.Obligations)
        {
            Validate(item.Evidence, sources);
            var source = sources.FirstOrDefault(x => x.Id == item.ClauseId);
            if (
                source is null
                || !item.Evidence.Any(x => x.ClauseId == item.ClauseId)
                || string.IsNullOrWhiteSpace(item.Party)
                || string.IsNullOrWhiteSpace(item.Action)
            )
                throw new AppException(502, "invalid_obligation", "Obligation is not grounded.");
            var quoted = string.Join(" ", item.Evidence.Select(e => e.Quote));
            if (!quoted.Contains(item.Party, StringComparison.OrdinalIgnoreCase))
                throw new AppException(
                    502,
                    "ungrounded_party",
                    "Obligation party is absent from the quotation."
                );
            var numbers = Regex
                .Matches(quoted, @"\d+(?:[.,]\d+)?")
                .Select(m => m.Value)
                .ToHashSet();
            if (
                Regex
                    .Matches(item.Action + " " + item.RelativeTerm, @"\d+(?:[.,]\d+)?")
                    .Any(m => !numbers.Contains(m.Value))
            )
                throw new AppException(
                    502,
                    "ungrounded_number",
                    "An obligation contains a number absent from its quotation."
                );
            if (
                item.DueDate is not null
                && (
                    !string.IsNullOrWhiteSpace(item.RelativeTerm)
                    || !item.Evidence.Any(x =>
                        x.Quote.Contains(
                            item.DueDate.Value.ToString("yyyy-MM-dd"),
                            StringComparison.Ordinal
                        )
                    )
                )
            )
                throw new AppException(
                    502,
                    "ungrounded_date",
                    "An absolute date must be explicit in evidence; relative terms remain relative."
                );
        }
        foreach (var item in result.Findings)
            ValidateFinding(item, sources);
    }

    public static void ValidateFinding(FindingCandidate item, ClauseSource[] sources)
    {
        Validate(item.Evidence, sources);
        if (
            item.Severity != "needs_review"
            || string.IsNullOrWhiteSpace(item.Code)
            || string.IsNullOrWhiteSpace(item.Explanation)
        )
            throw new AppException(
                502,
                "invalid_finding",
                "A finding must be an observation requiring review."
            );
    }
}
