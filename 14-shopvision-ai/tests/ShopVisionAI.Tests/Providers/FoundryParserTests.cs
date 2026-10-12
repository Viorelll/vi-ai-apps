using System.Text.Json;
using ShopVisionAI.Ai.Providers;

namespace ShopVisionAI.Tests.Providers;

public sealed class FoundryParserTests
{
    [Fact]
    public void ParsesStructuredOutput()
    {
        using var root = JsonDocument.Parse(
            JsonSerializer.Serialize(
                new
                {
                    status = "completed",
                    output = new[]
                    {
                        new
                        {
                            type = "message",
                            content = new[]
                            {
                                new
                                {
                                    type = "output_text",
                                    text = JsonSerializer.Serialize(
                                        new
                                        {
                                            status = "Completed",
                                            observations = new[]
                                            {
                                                new
                                                {
                                                    sourceId = "image:seed",
                                                    kind = "visible",
                                                    attribute = "color",
                                                    value = "black",
                                                    region = (object?)null,
                                                },
                                            },
                                        }
                                    ),
                                },
                            },
                        },
                    },
                    usage = new { input_tokens = 100, output_tokens = 40 },
                }
            )
        );
        var result = FoundryVisionProvider.Parse(root.RootElement, "vision-model", "image:seed");
        Assert.Single(result.Observations);
        Assert.Equal(100, result.InputTokens);
        Assert.False(result.Demonstration);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("failed")]
    public void IncompleteRejected(string state)
    {
        using var root = JsonDocument.Parse(JsonSerializer.Serialize(new { status = state }));
        Assert.Throws<ArgumentException>(() =>
            FoundryVisionProvider.Parse(root.RootElement, "model", "image:seed")
        );
    }

    [Fact]
    public void FabricatedSourceRejected()
    {
        using var root = JsonDocument.Parse(
            JsonSerializer.Serialize(
                new
                {
                    status = "completed",
                    output = new[]
                    {
                        new
                        {
                            type = "message",
                            content = new[]
                            {
                                new
                                {
                                    type = "output_text",
                                    text = "{\"status\":\"Completed\",\"observations\":[{\"sourceId\":\"fake\",\"kind\":\"visible\",\"attribute\":\"color\",\"value\":\"black\",\"region\":null}]}",
                                },
                            },
                        },
                    },
                    usage = new { input_tokens = 1, output_tokens = 1 },
                }
            )
        );
        Assert.Throws<ArgumentException>(() =>
            FoundryVisionProvider.Parse(root.RootElement, "model", "image:seed")
        );
    }
}
