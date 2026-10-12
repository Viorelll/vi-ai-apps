using Microsoft.Extensions.Options;
using SupportAI.Ai.Models;
using SupportAI.Ai.Services;
using SupportAI.Domain.Models;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class RetryTests
{
    [Fact]
    public async Task PreservesProviderRetryAfterWithoutHiddenHttpRetries()
    {
        var client = new FoundryClient(new HttpClient(new RateLimitHandler()), Options.Create(new FoundryOptions { OpenAiEndpoint = "https://example.openai.azure.com/openai/v1/", ApiKey = "test-key" }));
        var error = await Assert.ThrowsAsync<SupportException>(() => client.PostAsync("responses", new { }, default));
        Assert.Equal(429, error.Status);
        Assert.Equal(45, error.RetryAfterSeconds);
    }
}
