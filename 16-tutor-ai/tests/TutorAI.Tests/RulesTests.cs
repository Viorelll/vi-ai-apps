using TutorAI.Domain.Rules;
using Xunit;

namespace TutorAI.Tests;

public sealed class RulesTests
{
    [Theory]
    [InlineData(1, 0.8, 0.8, 2)]
    [InlineData(2, 0.4, 0.4, 1)]
    [InlineData(3, 1, 1, 3)]
    [InlineData(1, 0, 0, 1)]
    [InlineData(2, 0.8, 0.4, 2)]
    public void AdaptsFromTwoScores(int level, double a, double b, int expected) =>
        Assert.Equal(expected, DifficultyRule.Next(level, [(decimal)a, (decimal)b]));

    [Fact]
    public void OneScoreDoesNotAdapt() => Assert.Equal(1, DifficultyRule.Next(1, [1m]));

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void RejectsInvalidLevel(int level) =>
        Assert.Throws<ArgumentException>(() => DifficultyRule.Next(level, []));

    [Fact]
    public void RejectsInvalidScore() =>
        Assert.Throws<ArgumentException>(() => DifficultyRule.Next(1, [1.1m]));

    [Theory]
    [InlineData("https://learn.microsoft.com/en-us/azure/search/", true)]
    [InlineData("http://learn.microsoft.com/", false)]
    [InlineData("https://learn.microsoft.com.evil.test/", false)]
    [InlineData("https://user@learn.microsoft.com/", false)]
    [InlineData("https://learn.microsoft.com:444/", false)]
    public void AllowsOnlyOfficialHttps(string url, bool expected) =>
        Assert.Equal(expected, OfficialSourcePolicy.IsAllowed(url));

    [Theory]
    [InlineData("Retriever", "Retriever", 1)]
    [InlineData("  retriever ", "Retriever", 1)]
    [InlineData("Browser", "Retriever", 0)]
    public void GradesObjectiveAnswers(string answer, string expected, int score) =>
        Assert.Equal(score, AnswerGrader.Grade(answer, expected));
}
