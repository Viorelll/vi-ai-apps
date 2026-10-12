using DataAI.Application.Analytics;
using DataAI.Domain.Contracts;
using DataAI.Domain.Entities;
using Xunit;

namespace DataAI.Tests;

public sealed class AggregateCalculatorTests
{
    private static readonly SalesRow[] SampleRows =
    [
        new() { SoldOn = new DateOnly(2026, 1, 5), Category = "Hardware", Quantity = 2m, UnitPrice = 10m, Currency = "EUR" },
        new() { SoldOn = new DateOnly(2026, 1, 6), Category = "Hardware", Quantity = 1m, UnitPrice = 5m, Currency = "EUR" },
        new() { SoldOn = new DateOnly(2026, 1, 7), Category = "Software", Quantity = 3m, UnitPrice = 7m, Currency = "EUR" }
    ];

    [Fact]
    public void Seed_revenue_by_category_matches_documented_acceptance_values()
    {
        var spec = new AggregateSpec("revenue", "category", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "EUR");
        AggregateCalculator.Validate(spec, SampleRows);

        var rows = AggregateCalculator.Calculate(SampleRows, spec);

        Assert.Collection(rows,
            hardware => { Assert.Equal("Hardware", hardware.Label); Assert.Equal(25m, hardware.Value); },
            software => { Assert.Equal("Software", software.Label); Assert.Equal(21m, software.Value); });
        Assert.Equal(46m, rows.Sum(x => x.Value));
    }

    [Fact]
    public void Date_bounds_are_half_open_and_currency_is_preserved()
    {
        var rows = new[]
        {
            new SalesRow { SoldOn = new DateOnly(2026, 1, 1), Category = "X", Quantity = 2m, UnitPrice = 3m, Currency = "EUR" },
            new SalesRow { SoldOn = new DateOnly(2026, 1, 2), Category = "X", Quantity = 5m, UnitPrice = 3m, Currency = "EUR" },
            new SalesRow { SoldOn = new DateOnly(2026, 1, 1), Category = "Y", Quantity = 8m, UnitPrice = 9m, Currency = "USD" }
        };
        var spec = new AggregateSpec("quantity", "day", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "EUR");

        var result = AggregateCalculator.Calculate(rows, spec);

        Assert.Equal([new AggregateBucket("2026-01-01", 2m)], result);
    }

    [Theory]
    [InlineData("SELECT * FROM SalesRows", "category", "EUR")]
    [InlineData("revenue", "arbitrary-column", "EUR")]
    [InlineData("revenue", "day", "not-a-currency")]
    public void Rejects_unallowlisted_aggregate_fields(string metric, string group, string currency)
    {
        var spec = new AggregateSpec(metric, group, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), currency);

        Assert.Throws<ArgumentException>(() => AggregateCalculator.Validate(spec, SampleRows));
    }
}
