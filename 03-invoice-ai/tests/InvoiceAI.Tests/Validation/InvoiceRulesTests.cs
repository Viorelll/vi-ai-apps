using InvoiceAI.Services.Validation;
using InvoiceAI.Tests.Fixtures;
namespace InvoiceAI.Tests.Validation;

public sealed class InvoiceRulesTests
{
    [Fact] public void ValidSeedHasNoIssues() => Assert.Empty(InvoiceRules.Validate(InvoiceFixture.Valid()));
    [Theory]
    [InlineData(119, -1)]
    [InlineData(121, 1)]
    [InlineData(120.02, 0.02)]
    [InlineData(0, -120)]
    public void DecimalMismatchIncludesDifference(decimal total, decimal difference)
    {
        var i = InvoiceFixture.Valid();
        i.Total = total;
        var issue = Assert.Single(InvoiceRules.Validate(i), x => x.Code == "TOTAL_MISMATCH");
        Assert.Contains($"\"difference\":{difference}", issue.DetailsJson);
    }
    [Theory]
    [InlineData(120)]
    [InlineData(120.01)]
    [InlineData(119.99)]
    public void ToleranceIsInclusive(decimal total)
    {
        var i = InvoiceFixture.Valid();
        i.Total = total;
        Assert.DoesNotContain(InvoiceRules.Validate(i), x => x.Code == "TOTAL_MISMATCH");
    }
    [Fact]
    public void UnknownAmountsAreNotZero()
    {
        var i = InvoiceFixture.Valid();
        i.Tax = null;
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "AMOUNT_REQUIRED");
    }
    [Theory]
    [InlineData(null, "CURRENCY_REQUIRED")]
    [InlineData("XYZ", "CURRENCY_INVALID")]
    [InlineData("mdl", "CURRENCY_INVALID")]
    public void InvalidCurrency(string? currency, string code)
    {
        var i = InvoiceFixture.Valid();
        i.Currency = currency;
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == code);
    }
    [Fact]
    public void MixedCurrency()
    {
        var i = InvoiceFixture.Valid();
        i.Lines[0].Currency = "EUR";
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "MIXED_CURRENCY");
    }
    [Fact]
    public void LineSumMismatch()
    {
        var i = InvoiceFixture.Valid();
        i.Lines[0].Amount = 99;
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "LINE_SUM_MISMATCH");
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "LINE_TOTAL_MISMATCH");
    }
    [Fact]
    public void MissingLineAmount()
    {
        var i = InvoiceFixture.Valid();
        i.Lines[0].Amount = null;
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "LINE_AMOUNT_REQUIRED");
    }
    [Theory]
    [InlineData(null)]
    [InlineData(0.2)]
    [InlineData(0.79)]
    public void LowOrMissingConfidenceBlocksExport(double? confidence)
    {
        var i = InvoiceFixture.Valid();
        i.Fields[0].Confidence = confidence is null ? null : (decimal)confidence;
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "CONFIDENCE_REVIEW");
    }
    [Fact]
    public void CorrectionConfirmsLowConfidence()
    {
        var i = InvoiceFixture.Valid();
        i.Fields[0].Confidence = null;
        i.Fields[0].CorrectedValueJson = "\"Seed\"";
        Assert.Empty(InvoiceRules.Validate(i));
    }
    [Fact]
    public void MissingEvidenceRequiresReview()
    {
        var i = InvoiceFixture.Valid();
        i.Fields[0].EvidenceJson = "[]";
        Assert.Contains(InvoiceRules.Validate(i), x => x.Code == "EVIDENCE_MISSING");
    }
    [Fact]
    public void ZeroDecimalAmountsAreKnown()
    {
        var i = InvoiceFixture.Valid();
        i.Subtotal = 0;
        i.Tax = 0;
        i.Total = 0;
        i.Lines[0].Amount = 0;
        i.Lines[0].UnitPrice = 0;
        Assert.Empty(InvoiceRules.Validate(i));
    }
    [Fact]
    public void DecimalAvoidsBinaryFloatError()
    {
        var i = InvoiceFixture.Valid();
        i.Subtotal = 0.1m;
        i.Tax = 0.2m;
        i.Total = 0.3m;
        i.Lines[0].Amount = 0.1m;
        i.Lines[0].Quantity = 1;
        i.Lines[0].UnitPrice = 0.1m;
        Assert.Empty(InvoiceRules.Validate(i));
    }
}
