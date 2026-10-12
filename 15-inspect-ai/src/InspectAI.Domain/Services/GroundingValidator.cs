using InspectAI.Domain.DTOs;
using InspectAI.Domain.Models;

namespace InspectAI.Domain.Services;

public static class GroundingValidator
{
    public const string Limitations =
        "Raport limitat la fotografiile și manualul selectate. Nu certifică funcționarea sau siguranța echipamentului. Verificările sunt propuneri pentru revizuire umană, nu comenzi de intervenție.";

    public static void ValidateRegion(Region r)
    {
        double[] v = [r.X, r.Y, r.Width, r.Height];
        if (
            r.CoordinateSpace != "normalized"
            || v.Any(x => !double.IsFinite(x))
            || r.X < 0
            || r.Y < 0
            || r.Width <= 0
            || r.Height <= 0
            || r.X + r.Width > 1
            || r.Y + r.Height > 1
        )
            Invalid("region_out_of_bounds");
    }

    public static void ValidateEvidence(ManualEvidence[] evidence, ManualSource[] sources)
    {
        if (evidence is null || evidence.Length is < 1 or > 10)
            Invalid("missing_manual_evidence");
        foreach (var e in evidence!)
        {
            var source = sources.SingleOrDefault(s => s.ChunkId == e.ChunkId);
            if (
                source is null
                || string.IsNullOrWhiteSpace(e.Quote)
                || e.Quote.Length > 4000
                || !source.Text.Contains(e.Quote, StringComparison.Ordinal)
            )
                Invalid("invalid_citation");
        }
    }

    public static void Validate(InspectionResult r, PhotoSource[] photos, ManualSource[] sources)
    {
        if (
            string.IsNullOrWhiteSpace(r.Summary)
            || r.Summary.Length > 8000
            || r.Unknowns is null
            || r.Unknowns.Length > 50
            || r.Unknowns.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 2000)
            || r.Findings is null
            || r.Findings.Length is < 1 or > 50
            || r.Checklist is null
            || r.Checklist.Length > 50
        )
            Invalid("invalid_report");
        if (
            r.Findings!.Select(x => x.Id).Distinct().Count() != r.Findings.Length
            || r.Checklist!.Select(x => x.Id).Distinct().Count() != r.Checklist.Length
        )
            Invalid("duplicate_result_id");
        foreach (var f in r.Findings)
        {
            if (
                f.Id == Guid.Empty
                || !photos.Any(p => p.DocumentId == f.DocumentId)
                || f.Kind is not ("visible" or "inferred" or "unknown")
                || string.IsNullOrWhiteSpace(f.Description)
                || f.Description.Length > 2000
                || (f.Kind == "inferred" && string.IsNullOrWhiteSpace(f.Reason))
            )
                Invalid("invalid_observation");
            if (
                f.Kind == "visible"
                && (
                    f.Description.Contains("internal corrosion", StringComparison.OrdinalIgnoreCase)
                    || f.Description.Contains(
                        "coroziune intern",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
                Invalid("internal_state_not_visible");
            if (f.Region is not null)
                ValidateRegion(f.Region);
        }
        foreach (var c in r.Checklist!)
        {
            if (
                c.Id == Guid.Empty
                || !r.Findings.Any(f => f.Id == c.FindingId)
                || string.IsNullOrWhiteSpace(c.Text)
                || c.Text.Length > 2000
            )
                Invalid("invalid_checklist");
            ValidateEvidence(c.Evidence, sources);
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid(string code) =>
        throw new AppException(502, code, "AI output failed evidence/schema validation.");
}
