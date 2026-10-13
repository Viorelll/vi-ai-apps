using BuildBudgetAI.Ai.Providers;
using BuildBudgetAI.Domain.Dto;
using Xunit;

namespace BuildBudgetAI.Tests;

public sealed class OfferExtractionTests
{
    [Fact]
    public void ExplicitTextPreservesMissingTransport()
    {
        var draft = OfferExtractionProvider.ParseExplicitText([
            new(
                "S1",
                Guid.NewGuid(),
                1,
                "Material: Izolație\nUnitate: m²\nPreț unitar: 5 EUR\nTaxe: incluse"
            ),
        ]);
        Assert.Equal("5", draft.Lines[0].Price);
        Assert.Null(draft.Lines[0].Transport);
        Assert.Equal("Included", draft.TaxBasis);
    }

    [Fact]
    public void InvalidCitationRejected()
    {
        var draft = new OfferDraft(
            null,
            "EUR",
            "Included",
            [new(null, null, "5", null, null, ["fabricated"])],
            "fake"
        );
        Assert.Throws<System.Text.Json.JsonException>(() =>
            OfferExtractionProvider.Validate(draft, [])
        );
    }
}
