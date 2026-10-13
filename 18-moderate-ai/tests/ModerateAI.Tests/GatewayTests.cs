using System.Net;
using Microsoft.Extensions.Options;
using ModerateAI.Ai.Options;
using ModerateAI.Ai.Providers;
using ModerateAI.Ai.Services;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Models;
using ModerateAI.Domain.Services;
using Xunit;

namespace ModerateAI.Tests;

public sealed class GatewayTests
{
    private static readonly ProviderOptions Config = new()
    {
        Mode = "Foundry",
        SafetyEndpoint = "https://safety.example/",
        SafetyApiKey = "test-safety-secret",
        OpenAiEndpoint = "https://model.example/openai/v1/",
        ApiKey = "test-foundry-secret",
        ChatDeployment = "test-chat",
    };
    private static SafetySnapshot Snapshot =>
        new(
            "test",
            "2024-09-01",
            "FourSeverityLevels",
            PolicyEvaluator.Categories.Select(c => new SafetyScore(c, 0)).ToArray(),
            true,
            "{}"
        );

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(401, false)]
    [InlineData(400, false)]
    public async Task ProviderErrorsHaveBoundedRetryClassification(int status, bool transient)
    {
        using var client = new HttpClient(
            new TestHttpHandler(_ => new HttpResponseMessage((HttpStatusCode)status))
        );
        var adapter = new ContentSafetyAdapter(
            client,
            Options.Create(Config),
            new TestObjectStore()
        );
        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.AnalyzeAsync(new(Guid.NewGuid(), "synthetic text", null, null), default)
        );
        Assert.Equal(transient, error.Transient);
    }

    [Fact]
    public async Task SafetyUsesCorrectEndpointHeadersAndOutputScale()
    {
        string? path = null;
        string? body = null;
        using var client = new HttpClient(
            new TestHttpHandler(r =>
            {
                path = r.RequestUri!.AbsolutePath;
                body = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Equal(
                    Config.SafetyApiKey,
                    r.Headers.GetValues("Ocp-Apim-Subscription-Key").Single()
                );
                return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
            })
        );
        var adapter = new ContentSafetyAdapter(
            client,
            Options.Create(Config),
            new TestObjectStore()
        );
        var result = await adapter.AnalyzeAsync(
            new(Guid.NewGuid(), "synthetic text", null, null),
            default
        );
        Assert.Equal("/contentsafety/text:analyze", path);
        Assert.Contains("FourSeverityLevels", body!);
        Assert.False(result.Complete);
    }

    [Theory]
    [InlineData("{\"status\":\"incomplete\"}", "incomplete_foundry_response")]
    [InlineData("{}", "incomplete_foundry_response")]
    [InlineData("not-json", "invalid_foundry_json")]
    public async Task FoundryNeverAcceptsIncompleteOrMalformedResults(string json, string code)
    {
        using var client = new HttpClient(
            new TestHttpHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) })
        );
        var service = new FoundryExplainer(client, Options.Create(Config));
        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            service.ExplainAsync(Snapshot, PolicyEvaluator.Evaluate(Snapshot), default)
        );
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task ModelReceivesOnlyScoresAndCannotChangePolicy()
    {
        string? body = null;
        using var client = new HttpClient(
            new TestHttpHandler(r =>
            {
                body = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"{\"explanation\":\"Toate categoriile au scorul zero.\"}"}]}],"usage":{"input_tokens":30,"output_tokens":10}}"""
                    ),
                };
            })
        );
        var service = new FoundryExplainer(client, Options.Create(Config));
        var policy = PolicyEvaluator.Evaluate(Snapshot);
        var result = await service.ExplainAsync(Snapshot, policy, default);
        Assert.Equal(40, result.InputTokens + result.OutputTokens);
        Assert.Contains("\"store\":false", body!);
        Assert.DoesNotContain("tools", body!);
        Assert.Equal("allow", policy.Decision);
    }

    [Fact]
    public async Task DemoArbitraryInputRequiresReview()
    {
        using var client = new HttpClient(
            new TestHttpHandler(_ => throw new Exception("Demo must not call provider"))
        );
        var options = Options.Create(new ProviderOptions { Mode = "Demo" });
        var service = new AnalysisService(
            new(client, options, new TestObjectStore()),
            new(client, options),
            options
        );
        var result = await service.AnalyzeAsync(
            new(Guid.NewGuid(), "arbitrary unclassified input", null, null),
            default
        );
        Assert.False(result.Snapshot.Complete);
        Assert.Equal("review", PolicyEvaluator.Evaluate(result.Snapshot).Decision);
    }

    [Fact]
    public void FoundryPlaceholdersFailStartupValidation()
    {
        Assert.False(ProviderOptions.Valid(new() { Mode = "Foundry" }));
        Assert.True(ProviderOptions.Valid(new() { Mode = "Demo" }));
    }
}
