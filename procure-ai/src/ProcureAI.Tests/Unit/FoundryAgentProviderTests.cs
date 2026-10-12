using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ProcureAI.Ai.Providers;
using ProcureAI.Ai.Tools;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Models;

namespace ProcureAI.Tests.Unit;

public sealed class FoundryAgentProviderTests
{
    private static readonly DomainStepRequest Request = new(Guid.NewGuid(), Guid.NewGuid(), "ProcureAI", Guid.NewGuid(), "needs", "hash", "v1",
        new AgentInputDto("Need", new CriteriaDto("EUR", 10, "units", 10), [], [], [], "hash"), []);
    private static FoundryAgentProvider Provider(Func<int, HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHttpHandler(respond)), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Foundry:OpenAiEndpoint"] = "https://fixture.openai.azure.com/openai/v1/", ["Foundry:ApiKey"] = "dummy-test-key" }).Build(), new ProcurementTools());
    private static HttpResponseMessage Json(object payload) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(payload)) };
    [Fact]
    public async Task ParsesResponsesMessagesAndUsage()
    {
        var provider = Provider((_, _) => Json(new
        {
            status = "completed",
            usage = new { input_tokens = 12, output_tokens = 8 },
            output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = JsonDefaults.Serialize(Request.Input.Criteria) } } } }
        }));
        var result = await provider.ExecuteAsync(Request, default);
        Assert.Equal(12, result.InputTokens); Assert.Equal(8, result.OutputTokens);
        Assert.Equal(Request.Input.Criteria, JsonDefaults.Deserialize<CriteriaDto>(result.OutputJson));
    }
    [Fact]
    public async Task IncompleteResponseIsNotAValidResult()
    {
        var provider = Provider((_, _) => Json(new { status = "incomplete", output = Array.Empty<object>() }));
        Assert.Equal("foundry_response_incomplete", (await Assert.ThrowsAsync<AppException>(() => provider.ExecuteAsync(Request, default))).Code);
    }
    [Fact]
    public async Task ExecutesAllowedFunctionAndReturnsMatchingCallId()
    {
        var provider = Provider((call, request) =>
        {
            if (call == 1) return Json(new
            {
                status = "completed",
                usage = new { input_tokens = 1, output_tokens = 1 },
                output = new[] { new { type = "function_call", name = "validate_requirements", arguments = "{}", call_id = "call-1" } }
            });
            var content = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("function_call_output", content); Assert.Contains("call-1", content);
            return Json(new
            {
                status = "completed",
                usage = new { input_tokens = 2, output_tokens = 2 },
                output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = "{}" } } } }
            });
        });
        var result = await provider.ExecuteAsync(Request, default); Assert.Equal(3, result.InputTokens);
    }
    [Fact]
    public async Task ToolLoopHasHardLimit()
    {
        var provider = Provider((_, _) => Json(new
        {
            status = "completed",
            usage = new { input_tokens = 1, output_tokens = 1 },
            output = new[] { new { type = "function_call", name = "validate_requirements", arguments = "{}", call_id = "call" } }
        }));
        Assert.Equal("tool_limit_exceeded", (await Assert.ThrowsAsync<AppException>(() => provider.ExecuteAsync(Request, default))).Code);
    }
    [Fact]
    public void ToolsCannotAcceptCommandsOrForeignNames()
    {
        Assert.Throws<AppException>(() => new ProcurementTools().Execute("place_order", "{}", Request));
        Assert.Throws<AppException>(() => new ProcurementTools().Execute("validate_requirements", "{\"url\":\"https://evil.invalid\"}", Request));
    }
    [Theory]
    [InlineData("http://resource.cognitiveservices.azure.com/contentunderstanding/results/1")]
    [InlineData("https://evil.invalid/contentunderstanding/results/1")]
    [InlineData("https://resource.cognitiveservices.azure.com:444/contentunderstanding/results/1")]
    public void PollingUrlCannotLeakProviderKey(string url) => Assert.Throws<AppException>(() => ExtractionProvider.ValidateOperation(url,
        new Uri("https://resource.cognitiveservices.azure.com")));
}
