using System.Text.Json;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Models;
using ContractAI.Infrastructure.Ai;
using ContractAI.Infrastructure.Extraction;
using ContractAI.Tests.Fakes;
using Microsoft.Extensions.Configuration;

namespace ContractAI.Tests.Unit;

public sealed class FoundryTests
{
    private static IConfiguration Config(string key = "test-key") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    { "Foundry:OpenAiEndpoint", "https://test.openai.azure.com/openai/v1/" },
                    { "Foundry:ApiKey", key },
                    { "Foundry:ChatDeployment", "test-deployment" },
                }
            )
            .Build();

    private static AiRequest Request() =>
        new(
            "question",
            Guid.NewGuid(),
            "HASH",
            "contract-v1",
            [
                new(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    1,
                    1,
                    "Plata în 15 zile."
                ),
            ],
            "Termen?"
        );

    [Fact]
    public async Task ParsesCompletedResponsesAndUsage()
    {
        var handler = new StubHandler(
            (message, _) =>
            {
                Assert.Equal(
                    "https://test.openai.azure.com/openai/v1/responses",
                    message.RequestUri!.ToString()
                );
                Assert.True(message.Headers.Contains("api-key"));
                return Task.FromResult(
                    """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"answer\":\"15 zile\",\"evidence\":[]}"}]}],"usage":{"input_tokens":20,"output_tokens":10}}"""
                );
            }
        );
        var result = await new FoundryClient(new(handler), Config()).ExecuteAsync(
            Request(),
            default
        );
        Assert.Equal(20, result.InputTokens);
        Assert.Equal(10, result.OutputTokens);
        Assert.Equal("test-deployment", result.Deployment);
    }

    [Fact]
    public async Task RejectsIncompleteResponse()
    {
        var handler = new StubHandler(
            (_, _) => Task.FromResult("""{"status":"incomplete","output":[]}""")
        );
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new FoundryClient(new(handler), Config()).ExecuteAsync(Request(), default)
        );
        Assert.Equal("incomplete_ai_response", ex.Code);
    }

    [Fact]
    public async Task MissingCredentialsDoNotFallbackToMockAi()
    {
        var handler = new StubHandler((_, _) => throw new Exception("Must not call provider"));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new FoundryClient(new(handler), Config("replace-with-key")).ExecuteAsync(
                Request(),
                default
            )
        );
        Assert.Equal("foundry_not_configured", ex.Code);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task ExecutesScopedToolAndAccumulatesUsage()
    {
        var input = Request();
        var handler = new StubHandler(
            async (message, index) =>
            {
                using var body = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
                if (index == 0)
                    return """{"status":"completed","output":[{"type":"function_call","name":"search_clauses","arguments":"{\"query\":\"plata\"}","call_id":"call-1"}],"usage":{"input_tokens":5,"output_tokens":2}}""";
                var toolOutput = body.RootElement.GetProperty("input")[2];
                Assert.Equal("call-1", toolOutput.GetProperty("call_id").GetString());
                Assert.Contains(
                    input.Sources[0].Id.ToString(),
                    toolOutput.GetProperty("output").GetString()
                );
                return """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"answer\":\"15 zile\",\"evidence\":[]}"}]}],"usage":{"input_tokens":8,"output_tokens":3}}""";
            }
        );
        var result = await new FoundryClient(new(handler), Config()).ExecuteAsync(input, default);
        Assert.Equal(13, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
    }

    [Fact]
    public async Task StopsToolLoopAtFiveCalls()
    {
        var handler = new StubHandler(
            (_, _) =>
                Task.FromResult(
                    """{"status":"completed","output":[{"type":"function_call","name":"search_clauses","arguments":"{\"query\":\"plata\"}","call_id":"loop"}],"usage":{"input_tokens":5,"output_tokens":2}}"""
                )
        );
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new FoundryClient(new(handler), Config()).ExecuteAsync(Request(), default)
        );
        Assert.Equal("tool_limit", ex.Code);
        Assert.Equal(6, handler.Count);
    }

    [Theory]
    [InlineData("https://evil.example/contentunderstanding/operations/1")]
    [InlineData("http://test.cognitiveservices.azure.com/contentunderstanding/operations/1")]
    [InlineData("https://test.cognitiveservices.azure.com/other/1")]
    public void RejectsUntrustedPollingUrl(string url) =>
        Assert.Throws<AppException>(() =>
            DocumentExtractor.ValidateOperationUrl(
                url,
                new("https://test.cognitiveservices.azure.com/")
            )
        );

    [Fact]
    public void ExtractsCorrectPageNumbersFromLayout()
    {
        using var json = JsonDocument.Parse(
            """{"result":{"contents":[{"markdown":"duplicate","pages":[{"pageNumber":3,"lines":[{"content":"Plata în 15 zile."}]}]}]}}"""
        );
        var page = Assert.Single(DocumentExtractor.ParsePages(json.RootElement));
        Assert.Equal(3, page.PageNumber);
        Assert.Equal("Plata în 15 zile.", page.Text);
    }

    [Fact]
    public void KeepsUnknownPageUnknown()
    {
        using var json = JsonDocument.Parse(
            """{"result":{"contents":[{"markdown":"Text fără pagină"}]}}"""
        );
        Assert.Null(Assert.Single(DocumentExtractor.ParsePages(json.RootElement)).PageNumber);
    }

    [Fact]
    public async Task RepairsMalformedJsonOnceThenFailsSafely()
    {
        var handler = new StubHandler(
            (_, _) =>
                Task.FromResult(
                    """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"this is not JSON"}]}],"usage":{"input_tokens":5,"output_tokens":2}}"""
                )
        );
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new FoundryClient(new(handler), Config()).ExecuteAsync(Request(), default)
        );
        Assert.Equal("invalid_ai_json", ex.Code);
        Assert.Equal(2, handler.Count);
    }
}
