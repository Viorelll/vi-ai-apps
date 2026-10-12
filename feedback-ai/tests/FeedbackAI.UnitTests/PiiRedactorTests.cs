using FeedbackAI.Application.Services;
using Xunit;

namespace FeedbackAI.UnitTests;

public sealed class PiiRedactorTests
{
    [Fact]
    public void MasksEmailPhoneAndPreservesSurroundingText()
    {
        var result = PiiRedactor.Redact("Scrieți-mi la Ana@example.com sau sunați +373 69123456, mulțumesc.");
        Assert.DoesNotContain("Ana@example.com", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("69123456", result);
        Assert.Contains("mulțumesc", result);
    }

    [Fact]
    public void KeepsUnicodeTextWhenItContainsNoPersonalInformation()
    {
        var result = PiiRedactor.Redact("Produsul este foarte rău 😔");
        Assert.Equal("Produsul este foarte rău 😔", result);
    }

    [Fact]
    public void TaxonomyRejectsModelInventedLabels()
    {
        Assert.False(Taxonomy.IsValid("topic", "refund-escalation"));
        Assert.True(Taxonomy.IsValid("topic", "delivery"));
        Assert.False(Taxonomy.IsValid("sentiment", "urgent"));
    }
}
