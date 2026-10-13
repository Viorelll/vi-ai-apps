using System.Text.Json;
using AccessReadAI.Services.Validation;

namespace AccessReadAI.Tests.Validation;

public sealed class EvaluationTests
{
    [Fact]
    public void EvaluatesVersionedDataset()
    {
        var cases = JsonDocument
            .Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "evaluation.json")))
            .RootElement;
        var total = 0;
        foreach (var item in cases.EnumerateArray())
        {
            Assert.Equal(
                item.GetProperty("valid").GetBoolean(),
                MeaningValidator.IsSafe(
                    item.GetProperty("original").GetString()!,
                    item.GetProperty("variant").GetString()!
                )
            );
            total++;
        }
        Assert.Equal(20, total);
    }
}
