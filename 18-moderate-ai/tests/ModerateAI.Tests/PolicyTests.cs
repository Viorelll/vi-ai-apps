using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Services;
using Xunit;

namespace ModerateAI.Tests;

public sealed class PolicyTests
{
    [Theory]
    [InlineData(0, "allow")]
    [InlineData(2, "review")]
    [InlineData(4, "block")]
    [InlineData(6, "block")]
    public void FourLevelPolicy(int severity, string expected)
    {
        Assert.Equal(expected, PolicyEvaluator.Evaluate(Snapshot(severity)).Decision);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(-1)]
    public void InvalidScaleValuesRequireReview(int severity)
    {
        Assert.Equal("review", PolicyEvaluator.Evaluate(Snapshot(severity)).Decision);
    }

    [Fact]
    public void MissingCategoryNeverAllows()
    {
        var s = Snapshot(0);
        Assert.Equal(
            "review",
            PolicyEvaluator.Evaluate(s with { Scores = s.Scores.Take(3).ToArray() }).Decision
        );
    }

    [Fact]
    public void DuplicateCategoryNeverAllows()
    {
        var s = Snapshot(0);
        Assert.Equal(
            "review",
            PolicyEvaluator
                .Evaluate(s with { Scores = [s.Scores[0], s.Scores[0], s.Scores[2], s.Scores[3]] })
                .Decision
        );
    }

    [Fact]
    public void EightLevelsCannotUseFourLevelPolicy()
    {
        Assert.Equal(
            "review",
            PolicyEvaluator
                .Evaluate(Snapshot(0) with { OutputScale = "EightSeverityLevels" })
                .Decision
        );
    }

    [Fact]
    public void IncompleteRequiresReview()
    {
        Assert.Equal(
            "review",
            PolicyEvaluator.Evaluate(Snapshot(0) with { Complete = false }).Decision
        );
    }

    [Fact]
    public void HashIsStableAndBindsScores()
    {
        var s = Snapshot(2);
        Assert.Equal(
            PolicyEvaluator.Evaluate(s).DecisionHash,
            PolicyEvaluator.Evaluate(s with { Scores = s.Scores.Reverse().ToArray() }).DecisionHash
        );
        Assert.NotEqual(
            PolicyEvaluator.Evaluate(s).DecisionHash,
            PolicyEvaluator.Evaluate(Snapshot(4)).DecisionHash
        );
    }

    private static SafetySnapshot Snapshot(int value) =>
        new(
            "test",
            "2024-09-01",
            "FourSeverityLevels",
            PolicyEvaluator
                .Categories.Select(c => new SafetyScore(c, c == "Violence" ? value : 0))
                .ToArray(),
            true,
            "{}"
        );
}
