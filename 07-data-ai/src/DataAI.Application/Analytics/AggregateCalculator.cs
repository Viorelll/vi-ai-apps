using DataAI.Domain.Contracts;
using DataAI.Domain.Entities;

namespace DataAI.Application.Analytics;

public static class AggregateCalculator
{
    public static void Validate(AggregateSpec spec, IReadOnlyCollection<SalesRow> datasetRows)
    {
        if (spec.Metric is not ("revenue" or "quantity") || spec.GroupBy is not ("category" or "day") || spec.From >= spec.ToExclusive || spec.Currency.Length != 3 || !datasetRows.Any(x => x.Currency == spec.Currency))
            throw new ArgumentException("invalid_aggregate_spec");
    }

    public static IReadOnlyList<AggregateBucket> Calculate(IEnumerable<SalesRow> source, AggregateSpec spec)
    {
        var filtered = source.Where(x => x.SoldOn >= spec.From && x.SoldOn < spec.ToExclusive && x.Currency == spec.Currency);
        var grouped = spec.GroupBy == "category"
            ? filtered.GroupBy(x => x.Category).Select(g => new AggregateBucket(g.Key, spec.Metric == "revenue" ? g.Sum(x => x.Quantity * x.UnitPrice) : g.Sum(x => x.Quantity)))
            : filtered.GroupBy(x => x.SoldOn.ToString("yyyy-MM-dd")).Select(g => new AggregateBucket(g.Key, spec.Metric == "revenue" ? g.Sum(x => x.Quantity * x.UnitPrice) : g.Sum(x => x.Quantity)));
        return grouped.OrderBy(x => x.Label, StringComparer.Ordinal).ToArray();
    }
}
