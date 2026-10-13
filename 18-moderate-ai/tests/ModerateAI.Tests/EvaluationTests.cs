using System.Text.Json;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Services;
using Xunit;

namespace ModerateAI.Tests;

public sealed class EvaluationTests
{
    public static IEnumerable<object[]> Cases =>
        JsonSerializer
            .Deserialize<EvaluationCase[]>(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "eval/cases.json")),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
            )!
            .Select(c => new object[] { c.Name, c.Scale, c.Complete, c.Scores, c.Expected });

    [Theory]
    [MemberData(nameof(Cases))]
    public void VersionedPolicyBaseline(
        string name,
        string scale,
        bool complete,
        SafetyScore[] scores,
        string expected
    )
    {
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(
            expected,
            PolicyEvaluator
                .Evaluate(
                    new SafetySnapshot("Synthetic", "2024-09-01", scale, scores, complete, "{}")
                )
                .Decision
        );
    }
}
