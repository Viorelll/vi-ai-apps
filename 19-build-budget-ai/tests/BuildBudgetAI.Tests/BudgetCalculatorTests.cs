using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Services;
using Xunit;

namespace BuildBudgetAI.Tests;

public sealed class BudgetCalculatorTests
{
    [Fact]
    public void ReferenceTotalIs570() =>
        Assert.Equal(570m, BudgetCalculator.CalculateKnownTotal(100, 10, 5, 20));

    [Theory]
    [InlineData(-1, 0, 5, 0)]
    [InlineData(1, -1, 5, 0)]
    [InlineData(1, 101, 5, 0)]
    [InlineData(1, 0, -1, 0)]
    [InlineData(1, 0, 1, -1)]
    public void InvalidInputsFail(decimal q, decimal w, decimal p, decimal t) =>
        Assert.Throws<DomainException>(() => BudgetCalculator.CalculateKnownTotal(q, w, p, t));

    [Fact]
    public void RoundsAwayFromZero() =>
        Assert.Equal(1.01m, BudgetCalculator.CalculateKnownTotal(1, 0, 1.005m, 0));

    [Fact]
    public void UnknownTransportIsNotZero()
    {
        var (n, o, l) = Fixture();
        l.Transport = null;
        var r = BudgetCalculator.Calculate(n, o, l, "EUR", "Included");
        Assert.Null(r.TotalDecimal);
        Assert.Contains("Transport necunoscut", r.Unknowns);
    }

    [Theory]
    [InlineData("m³", "EUR", "Included", true)]
    [InlineData("m²", "USD", "Included", true)]
    [InlineData("m²", "EUR", "Excluded", true)]
    [InlineData("m²", "EUR", "Included", false)]
    public void IncompatibleOffersHaveNoTotal(
        string unit,
        string currency,
        string basis,
        bool confirmed
    )
    {
        var (n, o, l) = Fixture();
        l.Unit = unit;
        o.Currency = currency;
        o.TaxBasis = basis;
        l.Confirmed = confirmed;
        Assert.Null(BudgetCalculator.Calculate(n, o, l, "EUR", "Included").TotalDecimal);
    }

    [Fact]
    public void PackageRoundsUpToWholePackages()
    {
        var (n, o, l) = Fixture();
        n.Unit = "buc";
        n.Quantity = 11;
        n.WastePercent = 0;
        l.Unit = "pachet";
        l.PackQuantity = 10;
        l.Price = 50;
        var r = BudgetCalculator.Calculate(n, o, l, "EUR", "Included");
        Assert.Equal("120.00", r.TotalDecimal);
        Assert.Equal("20", r.AdjustedQuantity);
    }

    [Fact]
    public void PackageWithoutSizeIsUnknown()
    {
        var (n, o, l) = Fixture();
        n.Unit = "buc";
        l.Unit = "pachet";
        Assert.Null(BudgetCalculator.Calculate(n, o, l, "EUR", "Included").TotalDecimal);
    }

    [Fact]
    public void OverflowIsNotSilentlyTruncated() =>
        Assert.Throws<OverflowException>(() =>
            BudgetCalculator.CalculateKnownTotal(decimal.MaxValue, 100, 2, 0)
        );

    private static (MaterialNeed, MaterialOffer, OfferLine) Fixture() =>
        new(
            new()
            {
                Label = "Izolație",
                Unit = "m²",
                Quantity = 100,
                WastePercent = 10,
            },
            new() { Currency = "EUR", TaxBasis = "Included" },
            new()
            {
                MaterialLabel = "Izolație",
                Unit = "m²",
                Price = 5,
                Transport = 20,
                Confirmed = true,
            }
        );
}
