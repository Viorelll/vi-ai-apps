using System.Net;
using System.Text;
using BuildBudgetAI.Ai.Models;
using BuildBudgetAI.Ai.Providers;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Models;
using Xunit;

namespace BuildBudgetAI.Tests;

public sealed class FoundryProviderTests
{
    private static HttpResponseMessage Json(string value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json"),
        };

    private static GenerateRequest Input() => new("Explică", "{}", []);

    private static FoundryOptions Options() =>
        new()
        {
            Mode = "Foundry",
            OpenAiEndpoint = "https://test.openai.azure.com/openai/v1/",
            ApiKey = "test",
            ChatDeployment = "test",
        };

    [Fact]
    public async Task ParsesCompletedResponseAndUsage()
    {
        using var http = new HttpClient(
            new FakeHandler(
                (_, _) =>
                    Json(
                        """{"status":"completed","usage":{"input_tokens":12,"output_tokens":5},"output":[{"type":"message","content":[{"type":"output_text","text":"{\"summary\":\"Nu există surse\",\"unknowns\":[],\"citationLabels\":[]}"}]}]}"""
                    )
            )
        );
        var r = await new FoundryProvider(http, Options()).GenerateAsync(Input(), default);
        Assert.Equal(12, r.InputTokens);
        Assert.Equal("Nu există surse", r.Summary);
    }

    [Fact]
    public async Task IncompleteResponseFails()
    {
        using var http = new HttpClient(
            new FakeHandler((_, _) => Json("""{"status":"incomplete","output":[]} """))
        );
        var e = await Assert.ThrowsAsync<DomainException>(() =>
            new FoundryProvider(http, Options()).GenerateAsync(Input(), default)
        );
        Assert.Equal("foundry_incomplete", e.Code);
    }

    [Fact]
    public async Task ToolRoundsAreBounded()
    {
        var handler = new FakeHandler(
            (_, _) =>
                Json(
                    """{"status":"completed","output":[{"type":"function_call","name":"read_budget_context","arguments":"{}","call_id":"c1"}]}"""
                )
        );
        using var http = new HttpClient(handler);
        var options = Options();
        options.MaxToolRounds = 1;
        var e = await Assert.ThrowsAsync<DomainException>(() =>
            new FoundryProvider(http, options).GenerateAsync(Input(), default)
        );
        Assert.Equal("tool_limit", e.Code);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task UnauthorizedCitationFails()
    {
        using var http = new HttpClient(
            new FakeHandler(
                (_, _) =>
                    Json(
                        """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"summary\":\"x\",\"unknowns\":[],\"citationLabels\":[\"fabricated\"]}"}]}]}"""
                    )
            )
        );
        Assert.Equal(
            "invalid_model_json",
            (
                await Assert.ThrowsAsync<DomainException>(() =>
                    new FoundryProvider(http, Options()).GenerateAsync(Input(), default)
                )
            ).Code
        );
    }

    [Fact]
    public async Task TokenBudgetIsEnforced()
    {
        using var http = new HttpClient(
            new FakeHandler(
                (_, _) =>
                    Json(
                        """{"status":"completed","usage":{"input_tokens":15001,"output_tokens":0},"output":[]} """
                    )
            )
        );
        Assert.Equal(
            "token_budget_exceeded",
            (
                await Assert.ThrowsAsync<DomainException>(() =>
                    new FoundryProvider(http, Options()).GenerateAsync(Input(), default)
                )
            ).Code
        );
    }

    [Fact]
    public async Task DemoDoesNotCallAzure()
    {
        var handler = new FakeHandler((_, _) => throw new Exception("must not call cloud"));
        using var http = new HttpClient(handler);
        var r = await new FoundryProvider(http, new()).GenerateAsync(Input(), default);
        Assert.Equal("demo-no-model", r.Deployment);
        Assert.Equal(0, handler.Calls);
    }
}
