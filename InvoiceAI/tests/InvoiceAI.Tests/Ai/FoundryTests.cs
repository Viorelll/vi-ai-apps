using System.Text.Json;
using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Ai.Providers.Foundry;
using InvoiceAI.Ai.Tools;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Tests.Fixtures;
namespace InvoiceAI.Tests.Ai;

public sealed class FoundryTests
{
    private static ExplainRequest Input() => new(Guid.NewGuid(), InvoiceFixture.Extraction(Guid.NewGuid()).Fields, [], []);
    private static ProviderOptions Options() => new() { Mode = "Azure", DocumentEndpoint = "https://example.cognitiveservices.azure.com/", DocumentKey = "test-key", OpenAiEndpoint = "https://example.openai.azure.com/openai/v1/", ApiKey = "test-key" };
    private static string Final(string source) => JsonSerializer.Serialize(new { status = "completed", usage = new { input_tokens = 10, output_tokens = 5 }, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(new { summary = "Verifică totalul.", suggestedCategory = (string?)null, sourceIds = new[] { source } }) } } } } });
    [Fact]
    public async Task ExtractsResponsesTextAndUsage()
    {
        var input = Input();
        var source = input.Fields[0].Evidence[0].SourceId;
        var handler = new FakeHttpHandler((_, _) => Final(source));
        var p = new FoundryExplanationProvider(new(handler), new(), Microsoft.Extensions.Options.Options.Create(Options()));
        var result = await p.ExplainAsync(input, default);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
        Assert.Equal(source, Assert.Single(result.SourceIds));
        Assert.Contains("\"store\":false", handler.Payloads[0]);
    }
    [Fact]
    public async Task RejectsInventedCitation()
    {
        var handler = new FakeHttpHandler((_, _) => Final("evil"));
        var p = new FoundryExplanationProvider(new(handler), new(), Microsoft.Extensions.Options.Options.Create(Options()));
        var e = await Assert.ThrowsAsync<GatewayException>(() => p.ExplainAsync(Input(), default));
        Assert.Equal("MODEL_OUTPUT_INVALID", e.Code);
    }
    [Fact]
    public async Task RejectsIncomplete()
    {
        var handler = new FakeHttpHandler((_, _) => """{"status":"incomplete","usage":{"input_tokens":1,"output_tokens":1},"output":[]}""");
        var p = new FoundryExplanationProvider(new(handler), new(), Microsoft.Extensions.Options.Options.Create(Options()));
        Assert.Equal("MODEL_INCOMPLETE", (await Assert.ThrowsAsync<GatewayException>(() => p.ExplainAsync(Input(), default))).Code);
    }
    [Fact]
    public async Task ExecutesToolAndReturnsSameCallId()
    {
        var input = Input();
        var handler = new FakeHttpHandler((n, _) => n == 0 ? """{"status":"completed","usage":{"input_tokens":1,"output_tokens":1},"output":[{"type":"function_call","name":"calculate_totals","arguments":"{}","call_id":"c1"}]}""" : Final(input.Fields[0].Evidence[0].SourceId));
        var p = new FoundryExplanationProvider(new(handler), new(), Microsoft.Extensions.Options.Options.Create(Options()));
        var result = await p.ExplainAsync(input, default);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(11, result.InputTokens);
        Assert.Contains("\"call_id\":\"c1\"", handler.Payloads[1]);
        Assert.Contains("function_call_output", handler.Payloads[1]);
    }
    [Fact]
    public async Task ToolRoundsStopLoop()
    {
        var o = Options();
        o.MaxToolRounds = 1;
        var handler = new FakeHttpHandler((_, _) => """{"status":"completed","usage":{"input_tokens":1,"output_tokens":1},"output":[{"type":"function_call","name":"calculate_totals","arguments":"{}","call_id":"c1"}]}""");
        var p = new FoundryExplanationProvider(new(handler), new(), Microsoft.Extensions.Options.Options.Create(o));
        Assert.Equal("TOOL_BUDGET_EXCEEDED", (await Assert.ThrowsAsync<GatewayException>(() => p.ExplainAsync(Input(), default))).Code);
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task TokenBudgetStops()
    {
        var o = Options();
        o.MaxRunTokens = 10;
        var input = Input();
        var handler = new FakeHttpHandler((_, _) => Final(input.Fields[0].Evidence[0].SourceId));
        var p = new FoundryExplanationProvider(new(handler), new(), Microsoft.Extensions.Options.Options.Create(o));
        var error = await Assert.ThrowsAsync<GatewayException>(() => p.ExplainAsync(input, default));
        Assert.Equal("MODEL_TOKEN_BUDGET", error.Code);
        Assert.Equal(10, error.InputTokens);
        Assert.Equal(5, error.OutputTokens);
    }
    [Fact]
    public void ToolRejectsArbitraryScopeAndUnknownNames()
    {
        var t = new InvoiceToolExecutor();
        Assert.Throws<GatewayException>(() => t.Execute("read_extracted_fields", "{\"invoiceId\":\"another\"}", Input()));
        Assert.Throws<GatewayException>(() => t.Execute("delete_document", "{}", Input()));
    }
    [Fact]
    public void ToolUsesCorrectedNumbers()
    {
        var input = Input();
        var fields = input.Fields.Select(x => x.Path == "total" ? x with { CorrectedValue = JsonSerializer.SerializeToElement(120) } : x).ToArray();
        using var result = JsonDocument.Parse(new InvoiceToolExecutor().Execute("calculate_totals", "{}", input with
        {
            Fields = fields
        }));
        Assert.Equal(0, result.RootElement.GetProperty("difference").GetDecimal());
    }
}
