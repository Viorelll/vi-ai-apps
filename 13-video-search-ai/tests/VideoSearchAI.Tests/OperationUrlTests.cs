using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Ai.Providers;
using VideoSearchAI.Ai.Providers.Extraction;

namespace VideoSearchAI.Tests;

public sealed class OperationUrlTests
{
    [Theory]
    [InlineData("http://resource.cognitiveservices.azure.com/contentunderstanding/operations/1")]
    [InlineData("https://evil.example/contentunderstanding/operations/1")]
    [InlineData("https://resource.cognitiveservices.azure.com/other")]
    [InlineData(
        "https://resource.cognitiveservices.azure.com:444/contentunderstanding/operations/1"
    )]
    public void RejectsUntrustedPollLocations(string url)
    {
        var client = new ContentUnderstandingClient(
            new HttpClient(),
            null!,
            Options.Create(
                new AiOptions { ContentEndpoint = "https://resource.cognitiveservices.azure.com/" }
            ),
            new()
        );
        Assert.Throws<ProviderException>(() => client.ValidateOperation(url));
    }

    [Fact]
    public void AcceptsSameHttpsProviderOperation()
    {
        var client = new ContentUnderstandingClient(
            new HttpClient(),
            null!,
            Options.Create(
                new AiOptions { ContentEndpoint = "https://resource.cognitiveservices.azure.com/" }
            ),
            new()
        );
        client.ValidateOperation(
            "https://resource.cognitiveservices.azure.com/contentunderstanding/analyzerResults/123?api-version=2025-11-01"
        );
    }
}
