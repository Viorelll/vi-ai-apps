using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Services;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Tests.Unit;

public sealed class OfferScorerTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly CriteriaDto Criteria = new("EUR", 10, "unități", 10);
    private static OfferDto Offer(Guid id, decimal net, decimal shipping, int days) =>
        new(id, Guid.NewGuid(), "Supplier", "EUR", 10, "unități", net, shipping, days, true, [Guid.NewGuid()]);

    [Fact]
    public void ReferenceCaseRanksAWithExactDecimalScores()
    {
        var result = new OfferScorer().Score([Offer(A, 1000, 50, 7), Offer(B, 980, 120, 9)], Criteria);
        Assert.Equal(A, result[0].Id); Assert.Equal(1050m, result[0].Cost); Assert.Equal(.79m, result[0].Score);
        Assert.Equal(1100m, result[1].Cost); Assert.Equal(.7m * 1050m / 1100m + .03m, result[1].Score);
    }
    [Theory]
    [InlineData("currency")]
    [InlineData("quantity")]
    [InlineData("unit")]
    [InlineData("net")]
    [InlineData("shipping")]
    [InlineData("days")]
    public void IncomparableOrNegativeValuesAreNeverConverted(string field)
    {
        var offer = Offer(A, 1000, 50, 7);
        offer = field switch
        {
            "currency" => offer with { Currency = "USD" },
            "quantity" => offer with { Quantity = 1 },
            "unit" => offer with { Unit = "boxes" },
            "net" => offer with { Net = -1 },
            "shipping" => offer with { Shipping = -1 },
            _ => offer with { LeadDays = -1 }
        };
        Assert.Equal("incomparable_offers", Assert.Throws<AppException>(() => new OfferScorer().Score([offer], Criteria)).Code);
    }
    [Theory]
    [InlineData("net")]
    [InlineData("shipping")]
    [InlineData("days")]
    [InlineData("currency")]
    [InlineData("quantity")]
    [InlineData("unit")]
    [InlineData("compliance")]
    public void MissingValuesBlockCalculation(string field)
    {
        var offer = Offer(A, 1000, 50, 7);
        offer = field switch
        {
            "net" => offer with { Net = null },
            "shipping" => offer with { Shipping = null },
            "days" => offer with { LeadDays = null },
            "currency" => offer with { Currency = null },
            "quantity" => offer with { Quantity = null },
            "unit" => offer with { Unit = null },
            _ => offer with { MeetsRequirements = null }
        };
        Assert.Equal("incomplete_offers", Assert.Throws<AppException>(() => new OfferScorer().Score([offer], Criteria)).Code);
    }
    [Fact]
    public void EligibilityAndDeadlineExcludeOffers()
    {
        var result = new OfferScorer().Score([Offer(A, 1, 1, 7) with { MeetsRequirements = false }, Offer(B, 1000, 50, 7)], Criteria);
        Assert.Single(result); Assert.Equal(B, result[0].Id);
        Assert.Throws<AppException>(() => new OfferScorer().Score([Offer(A, 1, 1, 11)], Criteria));
    }
    [Fact]
    public void ZeroCostIsNotAnInventedFreeOffer() => Assert.Equal("cost_required",
        Assert.Throws<AppException>(() => new OfferScorer().Score([Offer(A, 0, 0, 1)], Criteria)).Code);
    [Fact]
    public void TiesAreDeterministicRegardlessOfInputOrder()
    {
        var result = new OfferScorer().Score([Offer(B, 100, 10, 7), Offer(A, 100, 10, 7)], Criteria);
        Assert.Equal(A, result[0].Id);
    }
}
