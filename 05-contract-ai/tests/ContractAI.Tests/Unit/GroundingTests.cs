using ContractAI.Domain.DTOs;
using ContractAI.Domain.Models;
using ContractAI.Domain.Services;

namespace ContractAI.Tests.Unit;

public sealed class GroundingTests
{
    private static ClauseSource Source(string text) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 2, text);

    [Theory]
    [InlineData(null, null)]
    [InlineData(15, null)]
    [InlineData(-1, "2026-01-01")]
    public void RelativeDateRemainsUnknownWithoutValidAnchor(int? days, string? anchor) =>
        Assert.Null(
            GroundingValidator.ResolveDueDate(days, anchor is null ? null : DateOnly.Parse(anchor))
        );

    [Fact]
    public void VerifiedCalendarAnchorResolvesDate() =>
        Assert.Equal(
            new DateOnly(2026, 1, 16),
            GroundingValidator.ResolveDueDate(15, new(2026, 1, 1))
        );

    [Fact]
    public void RejectsForeignVersionCitation()
    {
        var source = Source("Plata în 15 zile.");
        Assert.Throws<AppException>(() =>
            GroundingValidator.Validate([new(Guid.NewGuid(), source.Text)], [source])
        );
    }

    [Fact]
    public void RejectsFabricatedQuote()
    {
        var source = Source("Plata în 15 zile.");
        Assert.Throws<AppException>(() =>
            GroundingValidator.Validate([new(source.Id, "Plata în 30 zile.")], [source])
        );
    }

    [Fact]
    public void AcceptsExactQuote()
    {
        var s = Source("Beneficiarul plătește în 15 zile de la recepție.");
        GroundingValidator.Validate([new(s.Id, "15 zile de la recepție")], [s]);
    }

    [Fact]
    public void RejectsInventedAbsoluteDate()
    {
        var s = Source("Beneficiarul plătește în 15 zile de la recepție.");
        var item = new ObligationCandidate(
            s.Id,
            "Beneficiarul",
            "Plată",
            new(2026, 1, 16),
            "15 zile",
            [new(s.Id, s.Text)]
        );
        Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateAnalysis(new("Plată", ["Data recepției"], [item], []), [s])
        );
    }

    [Fact]
    public void AcceptsRelativeTermWithoutAbsoluteDate()
    {
        var s = Source("Beneficiarul plătește în 15 zile de la recepție.");
        var item = new ObligationCandidate(
            s.Id,
            "Beneficiarul",
            "Plată",
            null,
            "15 zile de la recepție",
            [new(s.Id, s.Text)]
        );
        GroundingValidator.ValidateAnalysis(new("Plată", ["Data recepției"], [item], []), [s]);
    }

    [Fact]
    public void RejectsLegalVerdictSeverity()
    {
        var s = Source("Text");
        Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateFinding(
                new("VALID", "compliant", "Verdict", [new(s.Id, s.Text)]),
                [s]
            )
        );
    }

    [Fact]
    public void RejectsInventedAmount()
    {
        var source = Source("Beneficiarul plătește suma de 1000 EUR în 15 zile.");
        var obligation = new ObligationCandidate(
            source.Id,
            "Beneficiarul",
            "Plată 2000 EUR",
            null,
            "15 zile",
            [new(source.Id, source.Text)]
        );
        var ex = Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateAnalysis(new("Plată", [], [obligation], []), [source])
        );
        Assert.Equal("ungrounded_number", ex.Code);
    }

    [Fact]
    public void RejectsInventedParty()
    {
        var source = Source("Beneficiarul plătește suma de 1000 EUR.");
        var obligation = new ObligationCandidate(
            source.Id,
            "Alt client",
            "Plată 1000 EUR",
            null,
            null,
            [new(source.Id, source.Text)]
        );
        Assert.Throws<AppException>(() =>
            GroundingValidator.ValidateAnalysis(new("Plată", [], [obligation], []), [source])
        );
    }
}
