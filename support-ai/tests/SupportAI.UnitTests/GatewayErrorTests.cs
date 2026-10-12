using Microsoft.Extensions.Configuration;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
using SupportAI.Infrastructure.Ai;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class GatewayErrorTests
{
    [Theory]
    [InlineData("foundry_not_configured")]
    [InlineData("foundry_request_failed")]
    public async Task DistinguishesMissingConfigurationFromTransient503AndAcceptsNullRetryAfter(string code)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Internal:Key", "test-key" } }).Build();
        var client = new HttpAiGateway(new HttpClient(new ErrorHttpHandler(code)) { BaseAddress = new Uri("https://internal.example/") }, config);
        var error = await Assert.ThrowsAsync<SupportException>(() => client.StartAsync(new ExtractionRequest(Guid.NewGuid(), "originals/id", "application/pdf", "test.pdf"), default));
        Assert.Equal(code, error.Code);
        Assert.Equal(503, error.Status);
        Assert.Null(error.RetryAfterSeconds);
    }
}
