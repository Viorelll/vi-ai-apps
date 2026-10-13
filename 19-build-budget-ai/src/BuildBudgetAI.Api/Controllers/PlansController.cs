using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Api.Controllers;

[ApiController, Route("api/build/plans")]
public sealed class PlansController(
    PlanService service,
    AppDbContext db,
    BuildBudgetAI.Domain.Interfaces.IAiClient ai
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(
            await db
                .Plans.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Currency,
                    x.Revision,
                    x.State,
                })
                .ToListAsync(ct)
        );

    [HttpPost]
    public async Task<IActionResult> Create(CreatePlanRequest r, CancellationToken ct)
    {
        var p = await service.CreateAsync(r, ct);
        return Created($"/api/build/plans/{p.Id}", new { p.Id, p.Revision });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var p = await service.LoadAsync(id, ct);
        var chunks = await db
            .Chunks.Where(x =>
                p.Sources.Select(s => s.DocumentId).Contains(x.DocumentId)
                && db.Documents.Any(d =>
                    d.Id == x.DocumentId
                    && d.Status == "Ready"
                    && d.ActiveGeneration == x.Generation
                )
            )
            .Select(x => new EvidenceDto(x.Id.ToString(), x.DocumentId, x.PageNumber, x.Text))
            .ToListAsync(ct);
        return Ok(
            new
            {
                p.Id,
                p.Name,
                p.Currency,
                p.State,
                p.Revision,
                needs = p.Needs.Select(x => new
                {
                    x.Id,
                    x.Label,
                    x.Unit,
                    quantity = BudgetCalculator.Number(x.Quantity),
                    wastePercent = BudgetCalculator.Number(x.WastePercent),
                }),
                offers = p.Offers.Select(o => new
                {
                    o.Id,
                    o.SupplierLabel,
                    o.Currency,
                    o.TaxBasis,
                    o.DocumentId,
                    lines = o.Lines.Select(l => new
                    {
                        l.Id,
                        l.MaterialLabel,
                        l.Unit,
                        price = BudgetCalculator.Number(l.Price),
                        transport = l.Transport.HasValue
                            ? BudgetCalculator.Number(l.Transport.Value)
                            : null,
                        packQuantity = l.PackQuantity.HasValue
                            ? BudgetCalculator.Number(l.PackQuantity.Value)
                            : null,
                        l.Confirmed,
                        evidence = System.Text.Json.JsonSerializer.Deserialize<List<EvidenceDto>>(
                            l.EvidenceJson
                        ),
                    }),
                }),
                sources = p.Sources.Select(x => new { x.DocumentId, x.Role }),
                evidence = chunks,
                budget = await service.LatestAsync(id, p.Revision, ct),
            }
        );
    }

    [HttpPut("{id:guid}/needs")]
    public async Task<IActionResult> Needs(Guid id, UpdateNeedsRequest r, CancellationToken ct)
    {
        await service.UpdateNeedsAsync(id, r, ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/offers")]
    public async Task<IActionResult> Offers(Guid id, OfferInput r, CancellationToken ct)
    {
        await service.AddOfferAsync(id, r, ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/sources")]
    public async Task<IActionResult> Sources(Guid id, SourceInput r, CancellationToken ct)
    {
        await service.AddSourceAsync(id, r, ct);
        return await Get(id, ct);
    }

    [HttpPost("{id:guid}/budget")]
    public async Task<IActionResult> Budget(Guid id, BudgetRequest r, CancellationToken ct) =>
        Ok(await service.BudgetAsync(id, r, ct));

    [HttpPost("{id:guid}/extract")]
    public async Task<IActionResult> Extract(Guid id, CancellationToken ct)
    {
        var p = await service.LoadAsync(id, ct);
        var ids = p.Sources.Where(x => x.Role == "offer").Select(x => x.DocumentId).ToArray();
        var evidence = await db
            .Chunks.Where(x =>
                ids.Contains(x.DocumentId)
                && db.Documents.Any(d =>
                    d.Id == x.DocumentId
                    && d.Status == "Ready"
                    && d.ActiveGeneration == x.Generation
                )
            )
            .OrderBy(x => x.DocumentId)
            .ThenBy(x => x.Ordinal)
            .Take(8)
            .Select(x => new EvidenceDto(x.Id.ToString(), x.DocumentId, x.PageNumber, x.Text))
            .ToListAsync(ct);
        if (evidence.Count == 0)
            throw new DomainException("ready_offer_source_required", 409);
        return Ok(await ai.ExtractOffersAsync(new(evidence), ct));
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var p = await service.LoadAsync(id, ct);
        var b =
            await service.LatestAsync(id, p.Revision, ct)
            ?? throw new DomainException("budget_required", 409);
        return File(
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    plan = p.Name,
                    budget = b,
                    assumptions = new[]
                    {
                        "Prețuri confirmate; fără conversie valutară implicită",
                        "Transportul necunoscut nu este zero",
                        "Fotografiile nu constituie măsurători structurale",
                    },
                    offers = p.Offers.Select(o => new
                    {
                        o.SupplierLabel,
                        o.Currency,
                        o.TaxBasis,
                        o.DocumentId,
                        lines = o.Lines.Select(l => new
                        {
                            l.MaterialLabel,
                            l.Unit,
                            price = BudgetCalculator.Number(l.Price),
                            l.EvidenceJson,
                        }),
                    }),
                }
            ),
            "application/json",
            "buildbudgetai-budget.json"
        );
    }
}
