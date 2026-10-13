using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Ai.Providers;
using VideoSearchAI.Ai.Providers.Foundry;
using VideoSearchAI.Ai.Tools;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Tests;

public sealed class FoundryContractTests
{
    private static AiOptions Options() =>
        new()
        {
            OpenAiEndpoint = "https://example.openai.azure.com/openai/v1/",
            ApiKey = "test",
            EmbeddingDimensions = 2,
            MaxToolRounds = 2,
        };

    [Fact]
    public async Task EmbeddingsReorderProviderItems()
    {
        var h = new FakeHttpHandler(
            (r, n) =>
                FakeHttpHandler.Json(
                    """{"data":[{"index":1,"embedding":[3,4]},{"index":0,"embedding":[1,2]}]}"""
                )
        );
        var client = new FoundryV1Client(
            new HttpClient(h),
            Microsoft.Extensions.Options.Options.Create(Options())
        );
        var result = await client.EmbedAsync(["a", "b"], default);
        Assert.Equal(1, result[0][0]);
        Assert.Equal(3, result[1][0]);
    }

    [Fact]
    public async Task DuplicateEmbeddingIndexesFail()
    {
        var h = new FakeHttpHandler(
            (r, n) =>
                FakeHttpHandler.Json(
                    """{"data":[{"index":0,"embedding":[1,2]},{"index":0,"embedding":[3,4]}]}"""
                )
        );
        var client = new FoundryV1Client(
            new HttpClient(h),
            Microsoft.Extensions.Options.Options.Create(Options())
        );
        await Assert.ThrowsAsync<ProviderException>(() => client.EmbedAsync(["a", "b"], default));
    }

    [Fact]
    public async Task WrongVectorDimensionsFail()
    {
        var h = new FakeHttpHandler(
            (r, n) => FakeHttpHandler.Json("""{"data":[{"index":0,"embedding":[1]}]}""")
        );
        var client = new FoundryV1Client(
            new HttpClient(h),
            Microsoft.Extensions.Options.Options.Create(Options())
        );
        await Assert.ThrowsAsync<ProviderException>(() => client.EmbedAsync(["a"], default));
    }

    [Fact]
    public async Task IncompleteResponseFails()
    {
        var o = Microsoft.Extensions.Options.Options.Create(Options());
        var h = new FakeHttpHandler(
            (r, n) =>
                FakeHttpHandler.Json(
                    """{"status":"incomplete","output":[],"usage":{"input_tokens":2,"output_tokens":1}}"""
                )
        );
        var generator = new AnswerGenerator(new(new HttpClient(h), o), o);
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            generator.GenerateAsync(new("question", []), default)
        );
        Assert.Equal("incomplete_response", e.Code);
    }

    [Fact]
    public async Task ParsesCompletedResponses()
    {
        var o = Microsoft.Extensions.Options.Options.Create(Options());
        var h = new FakeHttpHandler(
            (r, n) =>
                FakeHttpHandler.Json(
                    """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"answer\":\"No evidence\",\"matches\":[]}"}]}],"usage":{"input_tokens":2,"output_tokens":1}}"""
                )
        );
        var answer = await new AnswerGenerator(new(new HttpClient(h), o), o).GenerateAsync(
            new("question", []),
            default
        );
        Assert.Equal("No evidence", answer.Answer);
        Assert.Equal(2, answer.InputTokens);
    }

    [Fact]
    public async Task ToolLoopIsBounded()
    {
        var o = Microsoft.Extensions.Options.Options.Create(Options());
        var h = new FakeHttpHandler(
            (r, n) =>
                FakeHttpHandler.Json(
                    """{"status":"completed","output":[{"type":"function_call","name":"search_video_segments","arguments":"{\"query\":\"index\"}","call_id":"call-1"}],"usage":{"input_tokens":2,"output_tokens":1}}"""
                )
        );
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            new AnswerGenerator(new(new HttpClient(h), o), o).GenerateAsync(
                new("question", []),
                default
            )
        );
        Assert.Equal("tool_round_limit", e.Code);
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public void ToolsCannotReadOutsideSourceScope() =>
        Assert.Throws<ProviderException>(() =>
            GroundedTools.Execute(
                "read_segment_context",
                $"{{\"segmentId\":\"{Guid.NewGuid()}\"}}",
                []
            )
        );

    [Fact]
    public async Task RateLimitPreservesRetryAfter()
    {
        var h = new FakeHttpHandler(
            (r, n) =>
            {
                var response = FakeHttpHandler.Json(
                    "{}",
                    System.Net.HttpStatusCode.TooManyRequests
                );
                response.Headers.RetryAfter = new(TimeSpan.FromSeconds(7));
                return response;
            }
        );
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            new FoundryV1Client(
                new HttpClient(h),
                Microsoft.Extensions.Options.Options.Create(Options())
            ).EmbedAsync(["text"], default)
        );
        Assert.Equal(429, e.Status);
        Assert.Equal(TimeSpan.FromSeconds(7), e.RetryAfter);
    }
}
