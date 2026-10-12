using System.Globalization;
using System.Text.Json;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Domain.Entities;
using ShopVisionAI.Domain.Rules;

namespace ShopVisionAI.Services.Matching;

public sealed class MatchingService
{
    public IReadOnlyList<Candidate> Rank(
        IReadOnlyList<Observation> observations,
        IEnumerable<CatalogProduct> catalog,
        DateTimeOffset now
    )
    {
        var visible = observations.Where(x => x.Kind == "visible").ToArray();
        if (visible.Length == 0)
            return [];
        return catalog
            .Select(p =>
            {
                var attrs = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    p.AttributesJson
                )!;
                var category = visible
                    .FirstOrDefault(x => x.Attribute == "category")
                    ?.Value.ToLowerInvariant();
                if (category != null && category != p.Category)
                    return (Product: p, Score: 0d);
                var matched = visible.Count(o =>
                    o.Attribute == "category"
                        ? o.Value.Equals(p.Category, StringComparison.OrdinalIgnoreCase)
                        : attrs.TryGetValue(o.Attribute, out var v)
                            && o.Value.Equals(v, StringComparison.OrdinalIgnoreCase)
                );
                return (Product: p, Score: (double)matched / visible.Length);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Sku)
            .Take(5)
            .Select(x =>
            {
                var p = x.Product;
                return new Candidate(
                    p.Sku,
                    p.Name,
                    p.Category,
                    Math.Round(x.Score, 3),
                    "Candidat bazat pe atribute vizibile și fișa seed; SKU neconfirmat.",
                    p.Price.ToString("0.00", CultureInfo.InvariantCulture),
                    p.Currency,
                    p.Quantity,
                    AvailabilityRules.Label(
                        p.Quantity,
                        p.CapturedAtUtc,
                        now,
                        TimeSpan.FromHours(24)
                    ),
                    p.CapturedAtUtc,
                    ["catalog:" + p.Sku, "stock:" + p.Sku],
                    "/api/visual/catalog/" + p.Sku
                );
            })
            .ToArray();
    }
}
