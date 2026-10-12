using Microsoft.Extensions.Configuration;
using SupportAI.Ai.Services;
using SupportAI.Application.Abstractions;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class ExtractionTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ContentUnderstanding:Endpoint"] = "https://example.cognitiveservices.azure.com/",
        ["ContentUnderstanding:ApiKey"] = "test-key"
    }).Build();
    [Fact]
    public async Task NormalizesContentUnderstandingTextAndProvenance()
    {
        var handler = new QueueHttpHandler("""{"status":"Succeeded","result":{"contents":[{"markdown":"Evidence text","startPageNumber":2,"startTimeMs":1000,"endTimeMs":2000}]}}""");
        var result = await new ContentUnderstandingService(new HttpClient(handler), null!, Config()).PollAsync("https://example.cognitiveservices.azure.com/contentunderstanding/analyzerResults/id?api-version=2025-11-01", default);
        Assert.Equal("Succeeded", result.Status);
        var source = Assert.Single(result.Sources);
        Assert.Equal("Evidence text", source.Text);
        Assert.Equal(2, source.PageNumber);
        Assert.Equal(1000, source.StartMs);
    }
    [Fact]
    public async Task PendingExtractionRetainsOperationForDurablePolling()
    {
        var handler = new QueueHttpHandler("""{"status":"Running"}""");
        var url = "https://example.cognitiveservices.azure.com/contentunderstanding/analyzerResults/id";
        var result = await new ContentUnderstandingService(new HttpClient(handler), null!, Config()).PollAsync(url, default);
        Assert.Equal("Running", result.Status);
        Assert.Equal(url, result.OperationUrl);
        Assert.Empty(result.Sources);
    }
}
