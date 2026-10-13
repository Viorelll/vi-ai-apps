using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Services;

public sealed class PlanService(AppDbContext db)
{
    public async Task<BuildPlan> LoadAsync(Guid id, CancellationToken ct) =>
        await db
            .Plans.Include(x => x.Needs)
            .Include(x => x.Offers)
                .ThenInclude(x => x.Lines)
            .Include(x => x.Sources)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("plan_not_found", 404);

    public static void Revision(BuildPlan p, long revision)
    {
        if (p.Revision != revision)
            throw new DomainException("revision_conflict", 409);
    }

    public async Task<BuildPlan> CreateAsync(CreatePlanRequest r, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(r.Name)
            || r.Name.Length > 200
            || !System.Text.RegularExpressions.Regex.IsMatch(r.Currency ?? "", "^[A-Z]{3}$")
        )
            throw new DomainException("invalid_plan");
        var p = new BuildPlan { Name = r.Name.Trim(), Currency = r.Currency! };
        db.Plans.Add(p);
        await db.SaveChangesAsync(ct);
        return p;
    }

    public async Task UpdateNeedsAsync(Guid id, UpdateNeedsRequest r, CancellationToken ct)
    {
        var p = await LoadAsync(id, ct);
        Revision(p, r.Revision);
        if (
            r.Needs.Count > 200
            || r.Needs.Select(x => x.Label.Trim().ToLowerInvariant()).Distinct().Count()
                != r.Needs.Count
        )
            throw new DomainException("invalid_needs");
        foreach (var n in r.Needs)
            BudgetCalculator.ValidateNeed(n);
        db.Needs.RemoveRange(p.Needs);
        p.Needs = r
            .Needs.Select(n => new MaterialNeed
            {
                PlanId = id,
                Label = n.Label.Trim(),
                Unit = n.Unit,
                Quantity = n.Quantity,
                WastePercent = n.WastePercent,
            })
            .ToList();
        db.Needs.AddRange(p.Needs);
        p.Revision++;
        p.State = "NeedsReview";
        await db.SaveChangesAsync(ct);
    }

    public async Task AddOfferAsync(Guid id, OfferInput r, CancellationToken ct)
    {
        var p = await LoadAsync(id, ct);
        Revision(p, r.Revision);
        if (
            string.IsNullOrWhiteSpace(r.SupplierLabel)
            || r.SupplierLabel.Length > 200
            || r.Lines.Count is < 1 or > 200
            || !System.Text.RegularExpressions.Regex.IsMatch(r.Currency ?? "", "^[A-Z]{3}$")
            || !new[] { "Included", "Excluded", "Unknown" }.Contains(r.TaxBasis)
        )
            throw new DomainException("invalid_offer");
        if (
            r.DocumentId.HasValue
            && !await db.Sources.AnyAsync(x => x.PlanId == id && x.DocumentId == r.DocumentId, ct)
        )
            throw new DomainException("source_out_of_scope");
        var offer = new MaterialOffer
        {
            PlanId = id,
            SupplierLabel = r.SupplierLabel.Trim(),
            Currency = r.Currency!,
            TaxBasis = r.TaxBasis,
            DocumentId = r.DocumentId,
        };
        foreach (var l in r.Lines)
        {
            if (
                string.IsNullOrWhiteSpace(l.MaterialLabel)
                || l.MaterialLabel.Length > 200
                || !BudgetCalculator.Units.Contains(l.Unit)
                || l.Price < 0
                || l.Price > 100000000
                || l.Transport < 0
                || l.Transport > 100000000
                || l.PackQuantity <= 0
                || l.PackQuantity > 100000000
            )
                throw new DomainException("invalid_offer_line");
            foreach (var e in l.Evidence ?? [])
            {
                if (
                    e.DocumentId != r.DocumentId
                    || !await db.Chunks.AnyAsync(
                        x =>
                            x.DocumentId == e.DocumentId
                            && x.Id.ToString() == e.SourceId
                            && x.Text == e.Text
                            && x.PageNumber == e.PageNumber
                            && db.Documents.Any(d =>
                                d.Id == x.DocumentId
                                && d.Status == "Ready"
                                && d.ActiveGeneration == x.Generation
                            ),
                        ct
                    )
                )
                    throw new DomainException("invalid_evidence");
            }
            if (r.DocumentId.HasValue && (l.Evidence?.Count ?? 0) == 0)
                throw new DomainException("evidence_required");
            offer.Lines.Add(
                new OfferLine
                {
                    OfferId = offer.Id,
                    MaterialLabel = l.MaterialLabel,
                    Unit = l.Unit,
                    Price = l.Price,
                    Transport = l.Transport,
                    PackQuantity = l.PackQuantity,
                    Confirmed = l.Confirmed,
                    EvidenceJson = JsonSerializer.Serialize(l.Evidence ?? []),
                }
            );
        }
        db.Offers.Add(offer);
        p.Revision++;
        p.State = "NeedsReview";
        await db.SaveChangesAsync(ct);
    }

    public async Task AddSourceAsync(Guid id, SourceInput r, CancellationToken ct)
    {
        var p = await LoadAsync(id, ct);
        Revision(p, r.Revision);
        if (
            !new[] { "offer", "photo", "spec" }.Contains(r.Role)
            || !await db.Documents.AnyAsync(x => x.Id == r.DocumentId && x.Status != "Deleting", ct)
        )
            throw new DomainException("invalid_source");
        if (p.Sources.Any(x => x.DocumentId == r.DocumentId))
            return;
        db.Sources.Add(
            new PlanSource
            {
                PlanId = id,
                DocumentId = r.DocumentId,
                Role = r.Role,
            }
        );
        p.Revision++;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BudgetDto> BudgetAsync(Guid id, BudgetRequest r, CancellationToken ct)
    {
        var p = await LoadAsync(id, ct);
        Revision(p, r.Revision);
        if (
            r.SelectedOfferLines.Count == 0
            || r.SelectedOfferLines.Select(x => x.NeedId).Distinct().Count()
                != r.SelectedOfferLines.Count
        )
            throw new DomainException("invalid_selection");
        var lines = new List<BudgetLineDto>();
        string? basis = null;
        foreach (var selection in r.SelectedOfferLines.OrderBy(x => x.NeedId))
        {
            var need =
                p.Needs.SingleOrDefault(x => x.Id == selection.NeedId)
                ?? throw new DomainException("need_not_found");
            var offer =
                p.Offers.SingleOrDefault(x => x.Lines.Any(l => l.Id == selection.OfferLineId))
                ?? throw new DomainException("offer_line_not_found");
            var line = offer.Lines.Single(x => x.Id == selection.OfferLineId);
            basis ??= offer.TaxBasis;
            if (
                offer.DocumentId.HasValue
                && !await db.Documents.AnyAsync(
                    x => x.Id == offer.DocumentId && x.Status == "Ready",
                    ct
                )
            )
                throw new DomainException("source_unavailable", 409);
            if (
                !string.Equals(
                    need.Label.Trim(),
                    line.MaterialLabel.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new DomainException("material_mismatch");
            lines.Add(BudgetCalculator.Calculate(need, offer, line, p.Currency, basis));
        }
        string hash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { p.Revision, lines }))
            )
        );
        var existing = await db.Budgets.SingleOrDefaultAsync(
            x => x.PlanId == id && x.InputsHash == hash,
            ct
        );
        if (existing != null)
            return Map(existing, p.Revision);
        var b = new BudgetSnapshot
        {
            PlanId = id,
            Revision = p.Revision,
            InputsHash = hash,
            LinesJson = JsonSerializer.Serialize(lines),
            Total = lines.Sum(x =>
                x.TotalDecimal == null
                    ? 0
                    : decimal.Parse(
                        x.TotalDecimal,
                        System.Globalization.CultureInfo.InvariantCulture
                    )
            ),
            Currency = p.Currency,
            Incomplete = lines.Any(x => x.Unknowns.Count > 0) || lines.Count != p.Needs.Count,
        };
        db.Budgets.Add(b);
        p.State = b.Incomplete ? "NeedsReview" : "BudgetReady";
        await db.SaveChangesAsync(ct);
        return Map(b, p.Revision);
    }

    public static BudgetDto Map(BudgetSnapshot b, long current) =>
        new(
            b.Id,
            b.Revision,
            BudgetCalculator.Money(b.Total),
            b.Currency,
            b.Incomplete,
            b.Revision != current,
            JsonSerializer.Deserialize<List<BudgetLineDto>>(b.LinesJson)!
        );

    public async Task<BudgetDto?> LatestAsync(Guid id, long revision, CancellationToken ct)
    {
        var b = await db
            .Budgets.Where(x => x.PlanId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        return b == null ? null : Map(b, revision);
    }
}
