using System.Text.Json;
using Microsoft.Extensions.Options;
using SupportAI.Ai.Models;
using SupportAI.Ai.Services;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class FoundryTests
{
    private static DomainStepRequest Input() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Order status", new("LAB-1001", "Delivered", "Monitor", DateTimeOffset.UtcNow, true), [new("S1", Guid.NewGuid(), Guid.NewGuid(), 1, "support-policy.md", "Policy", null, null, null)], []);
    private static FoundrySupportService Service(QueueHttpHandler handler, int budget = 15000)
    {
        var options = Options.Create(new FoundryOptions { OpenAiEndpoint = "https://example.openai.azure.com/openai/v1/", ApiKey = "test-key", MaxRunTokens = budget });
        return new(new(new HttpClient(handler), options), new(), options);
    }
    private static string Answer() => JsonSerializer.Serialize(new { status = "completed", usage = new { input_tokens = 10, output_tokens = 20 }, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = "{\"answer\":\"Delivered\",\"status\":\"answered\",\"citationLabels\":[\"S1\"],\"proposedAction\":null}" } } } } });
    [Fact]
    public async Task ParsesResponsesAndAccumulatesUsage()
    {
        var handler = new QueueHttpHandler(Answer());
        var result = await Service(handler).RunAsync(Input(), default);
        Assert.Equal("Delivered", result.Output.Answer);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(20, result.OutputTokens);
    }
    [Fact]
    public async Task ExecutesReadToolAndReturnsMatchingCallId()
    {
        var call = """{"status":"completed","usage":{"input_tokens":1,"output_tokens":2},"output":[{"type":"function_call","name":"get_order_status","call_id":"call-1","arguments":"{\"reference\":\"LAB-1001\"}"}]}""";
        var handler = new QueueHttpHandler(call, Answer());
        var result = await Service(handler).RunAsync(Input(), default);
        Assert.Equal(11, result.InputTokens);
        Assert.Contains("function_call_output", handler.Requests[1]);
        Assert.Contains("call-1", handler.Requests[1]);
        Assert.Contains("Delivered", handler.Requests[1]);
    }
    [Fact]
    public async Task RejectsIncompleteResponses()
    {
        var handler = new QueueHttpHandler("""{"status":"incomplete","usage":{"input_tokens":1,"output_tokens":1},"output":[]}""");
        Assert.Equal("ai_response_incomplete", (await Assert.ThrowsAsync<SupportException>(() => Service(handler).RunAsync(Input(), default))).Code);
    }
    [Fact]
    public async Task StopsAtTokenBudget()
    {
        var handler = new QueueHttpHandler(Answer());
        Assert.Equal("ai_token_budget_exceeded", (await Assert.ThrowsAsync<SupportException>(() => Service(handler, 5).RunAsync(Input(), default))).Code);
    }
    [Fact]
    public async Task RepairsInvalidJsonOnlyOnce()
    {
        var bad = """{"status":"completed","usage":{"input_tokens":1,"output_tokens":1},"output":[{"type":"message","content":[{"type":"output_text","text":"invalid"}]}]}""";
        var handler = new QueueHttpHandler(bad, bad);
        await Assert.ThrowsAsync<SupportException>(() => Service(handler).RunAsync(Input(), default));
        Assert.Equal(2, handler.Requests.Count);
    }
    [Fact]
    public void ToolCannotReadAnotherOrder()
    {
        using var args = JsonDocument.Parse("""{"reference":"LAB-9999"}""");
        Assert.Throws<SupportException>(() => new SupportToolExecutor().Execute("get_order_status", args.RootElement, Input()));
    }
    [Theory]
    [InlineData("https://evil.example/contentunderstanding/result")]
    [InlineData("http://example.cognitiveservices.azure.com/contentunderstanding/result")]
    [InlineData("https://example.cognitiveservices.azure.com/not-an-operation")]
    public void RejectsOperationUrlOutsideProvider(string url) => Assert.Throws<SupportException>(() => ContentUnderstandingService.ValidateOperation(url, new Uri("https://example.cognitiveservices.azure.com/")));
}
