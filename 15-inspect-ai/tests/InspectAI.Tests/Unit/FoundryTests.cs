using System.Text.Json;
using InspectAI.Domain.DTOs;
using InspectAI.Domain.Models;
using InspectAI.Infrastructure.Ai;
using InspectAI.Infrastructure.Extraction;
using InspectAI.Tests.Fakes;
using Microsoft.Extensions.Configuration;

namespace InspectAI.Tests.Unit;

public sealed class FoundryTests
{
    private static IConfiguration Config =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Foundry:OpenAiEndpoint"] = "https://example.openai.azure.com/openai/v1/",
                    ["Foundry:ApiKey"] = "test-only",
                    ["Foundry:ChatDeployment"] = "vision-test",
                }
            )
            .Build();
    private static AiRequest Input
    {
        get
        {
            var p = Guid.NewGuid();
            return new(
                "analyze",
                Guid.NewGuid(),
                "hash",
                "inspect-v1",
                [new(p, "originals/" + p, "image/png", "hash")],
                [new(Guid.NewGuid(), Guid.NewGuid(), 1, "Inspect stain", 1, "hash")],
                "P-100",
                "1.0"
            );
        }
    }

    private static string Response(string json) =>
        JsonSerializer.Serialize(
            new
            {
                status = "completed",
                usage = new { input_tokens = 10, output_tokens = 20 },
                output = new[]
                {
                    new
                    {
                        type = "message",
                        content = new[] { new { type = "output_text", text = json } },
                    },
                },
            }
        );

    [Fact]
    public async Task SendsVisionAndScopedTool()
    {
        var handler = new StubHandler(
            async (req, _) =>
            {
                using var j = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                Assert.Equal("vision-test", j.RootElement.GetProperty("model").GetString());
                Assert.False(j.RootElement.GetProperty("store").GetBoolean());
                Assert.Contains(
                    j.RootElement.GetProperty("input")[0].GetProperty("content").EnumerateArray(),
                    c => c.GetProperty("type").GetString() == "input_image"
                );
                Assert.Equal(
                    "search_manual",
                    j.RootElement.GetProperty("tools")[0].GetProperty("name").GetString()
                );
                return Response("{}");
            }
        );
        var c = new FoundryClient(new HttpClient(handler), Config, new MemoryObjectStore());
        var result = await c.ExecuteAsync(Input, default);
        Assert.Equal(30, result.InputTokens + result.OutputTokens);
    }

    [Fact]
    public async Task RejectsIncomplete()
    {
        var c = new FoundryClient(
            new HttpClient(
                new StubHandler((_, _) => Task.FromResult("{\"status\":\"incomplete\"}"))
            ),
            Config,
            new MemoryObjectStore()
        );
        var e = await Assert.ThrowsAsync<AppException>(() => c.ExecuteAsync(Input, default));
        Assert.Equal("incomplete_ai_response", e.Code);
    }

    [Fact]
    public async Task RepairsMalformedJsonOnce()
    {
        var h = new StubHandler((_, n) => Task.FromResult(Response(n == 0 ? "not json" : "{}")));
        var c = new FoundryClient(new HttpClient(h), Config, new MemoryObjectStore());
        await c.ExecuteAsync(Input, default);
        Assert.Equal(2, h.Count);
    }

    [Fact]
    public async Task RejectsToolOverflow()
    {
        var h = new StubHandler(
            (_, _) =>
                Task.FromResult(
                    "{\"status\":\"completed\",\"output\":[{\"type\":\"function_call\",\"name\":\"search_manual\",\"call_id\":\"x\",\"arguments\":\"{\\\"query\\\":\\\"stain\\\"}\"}]}"
                )
        );
        var c = new FoundryClient(new HttpClient(h), Config, new MemoryObjectStore());
        var e = await Assert.ThrowsAsync<AppException>(() => c.ExecuteAsync(Input, default));
        Assert.Equal("tool_limit", e.Code);
        Assert.Equal(6, h.Count);
    }

    [Fact]
    public async Task PlaceholderDoesNotReturnFakeAnalysis()
    {
        var conf = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Foundry:ApiKey"] = "replace-with-key" }
            )
            .Build();
        var c = new FoundryClient(new HttpClient(), conf, new MemoryObjectStore());
        var e = await Assert.ThrowsAsync<AppException>(() => c.ExecuteAsync(Input, default));
        Assert.Equal("foundry_not_configured", e.Code);
    }

    [Fact]
    public void RejectsForeignOperationHost() =>
        Assert.Throws<AppException>(() =>
            DocumentExtractor.ValidateOperationUrl(
                "https://evil.test/contentunderstanding/result",
                new("https://example.cognitiveservices.azure.com/")
            )
        );

    [Theory]
    [InlineData(System.Net.HttpStatusCode.TooManyRequests, 503)]
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(System.Net.HttpStatusCode.Unauthorized, 502)]
    public async Task ProviderStatusAndRetryAfterArePreserved(
        System.Net.HttpStatusCode status,
        int expected
    )
    {
        var client = new FoundryClient(
            new HttpClient(new StatusHandler(status)),
            Config,
            new MemoryObjectStore()
        );
        var ex = await Assert.ThrowsAsync<AppException>(() => client.ExecuteAsync(Input, default));
        Assert.Equal(expected, ex.Status);
        Assert.Equal(17, ex.RetryAfterSeconds);
    }
}
