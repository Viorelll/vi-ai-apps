using ModerateAI.Ai.Providers;
using ModerateAI.Domain.Models;
using Xunit;

namespace ModerateAI.Tests;

public sealed class SafetyMapperTests
{
    [Fact]
    public void CompleteResponseMapsWithoutRescaling()
    {
        var s = SafetyResponseMapper.Map(
            """{"categoriesAnalysis":[{"category":"Hate","severity":0},{"category":"Sexual","severity":2},{"category":"SelfHarm","severity":4},{"category":"Violence","severity":6}]}"""
        );
        Assert.True(s.Complete);
        Assert.Equal("FourSeverityLevels", s.OutputScale);
        Assert.Equal(6, s.Scores.Single(x => x.Category == "Violence").Severity);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"categoriesAnalysis\":[]}")]
    [InlineData("{\"categoriesAnalysis\":[{\"category\":\"Violence\",\"severity\":7}]}")]
    public void IncompleteProviderResponseIsExplicit(string json)
    {
        Assert.False(SafetyResponseMapper.Map(json).Complete);
    }

    [Fact]
    public void MalformedJsonFails()
    {
        Assert.Throws<ProviderException>(() => SafetyResponseMapper.Map("not json"));
    }

    [Fact]
    public void CombinedIncompleteModalityCannotAllow()
    {
        var a = SafetyResponseMapper.Map("{}");
        Assert.False(SafetyResponseMapper.Combine([a, a]).Complete);
    }

    [Fact]
    public void CombinedScalesCannotBeSubstituted()
    {
        var four = SafetyResponseMapper.Map(
            """{"categoriesAnalysis":[{"category":"Hate","severity":0},{"category":"Sexual","severity":0},{"category":"SelfHarm","severity":0},{"category":"Violence","severity":0}]}"""
        );
        Assert.False(
            SafetyResponseMapper
                .Combine([four, four with { OutputScale = "EightSeverityLevels" }])
                .Complete
        );
    }
}
