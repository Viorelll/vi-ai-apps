using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Database;
using ShopVisionAI.Domain.Entities;
using ShopVisionAI.Domain.Rules;
using ShopVisionAI.Services.Errors;

namespace ShopVisionAI.Api.Controllers;

[ApiController]
[Route("api/visual/catalog")]
public sealed class CatalogController(AppDbContext db) : ControllerBase
{
    private async Task<CatalogProduct> Find(string sku, CancellationToken ct) =>
        await db.CatalogProducts.AsNoTracking().SingleOrDefaultAsync(x => x.Sku == sku, ct)
        ?? throw new DomainException("sku_not_found", 404);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(
            await db
                .CatalogProducts.AsNoTracking()
                .OrderBy(x => x.Sku)
                .Select(x => new
                {
                    x.Sku,
                    x.Name,
                    x.Category,
                    x.Sheet,
                    x.CatalogVersion,
                })
                .ToArrayAsync(ct)
        );

    [HttpGet("{sku}")]
    public async Task<IActionResult> Get(string sku, CancellationToken ct)
    {
        var p = await Find(sku, ct);
        return Ok(
            new
            {
                p.Sku,
                p.Name,
                p.Category,
                p.Sheet,
                p.AttributesJson,
                price = p.Price.ToString("0.00", CultureInfo.InvariantCulture),
                p.Currency,
                p.CatalogVersion,
            }
        );
    }

    [HttpGet("{sku}/stock")]
    public async Task<IActionResult> Stock(string sku, CancellationToken ct)
    {
        var p = await Find(sku, ct);
        return Ok(
            new
            {
                p.Sku,
                p.Quantity,
                p.CapturedAtUtc,
                sourceVersion = p.CatalogVersion,
                label = AvailabilityRules.Label(
                    p.Quantity,
                    p.CapturedAtUtc,
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromHours(24)
                ),
            }
        );
    }
}
