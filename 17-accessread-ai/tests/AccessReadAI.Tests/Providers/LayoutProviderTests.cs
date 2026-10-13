using System.Net;
using System.Text;
using AccessReadAI.Ai.Models;
using AccessReadAI.Ai.Providers;
using AccessReadAI.Contracts;
using Microsoft.Extensions.Options;

namespace AccessReadAI.Tests.Providers;

public sealed class LayoutProviderTests
{
    [Fact]
    public async Task StartsBinaryAnalysisAndReturnsOperationForPersistence()
    {
        var bytes = Encoding.UTF8.GetBytes("%PDF-artificial");
        var http = new HttpClient(
            new FakeHttpHandler(async request =>
            {
                Assert.Contains("api-version=2024-11-30", request.RequestUri!.Query);
                Assert.Equal(
                    "application/octet-stream",
                    request.Content!.Headers.ContentType!.MediaType
                );
                Assert.Equal(bytes, await request.Content.ReadAsByteArrayAsync());
                Assert.True(request.Headers.Contains("Ocp-Apim-Subscription-Key"));
                var response = new HttpResponseMessage(HttpStatusCode.Accepted);
                response.Headers.Add(
                    "Operation-Location",
                    "https://resource.test/documentintelligence/documentModels/prebuilt-layout/analyzeResults/result?api-version=2024-11-30"
                );
                return response;
            })
        );
        var extractor = new LayoutExtractor(
            http,
            new FakeObjectStore(bytes),
            Options.Create(
                new ProviderOptions { Demo = false, LayoutEndpoint = "https://resource.test/" }
            )
        );
        var result = await extractor.ExtractAsync(
            new(Guid.NewGuid(), "originals/test", "application/pdf", null),
            default
        );
        Assert.Equal("Running", result.State);
        Assert.NotNull(result.OperationUrl);
        Assert.Empty(result.Blocks);
    }

    [Fact]
    public async Task PollsWithoutStartingNewOperationAndMarksLowConfidenceForReview()
    {
        var http = new HttpClient(
            new FakeHttpHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"status":"succeeded","analyzeResult":{"pages":[{"pageNumber":1,"words":[{"content":"10","confidence":0.5}]}],"paragraphs":[{"content":"Nu depășiți 10 unități.","boundingRegions":[{"pageNumber":1,"polygon":[0,0,1,0,1,1,0,1]}]}]}}"""
                        ),
                    }
                );
            })
        );
        var extractor = new LayoutExtractor(
            http,
            new FakeObjectStore([]),
            Options.Create(
                new ProviderOptions { Demo = false, LayoutEndpoint = "https://resource.test/" }
            )
        );
        var result = await extractor.ExtractAsync(
            new(
                Guid.NewGuid(),
                "originals/test",
                "application/pdf",
                "https://resource.test/documentintelligence/documentModels/prebuilt-layout/analyzeResults/result?api-version=2024-11-30"
            ),
            default
        );
        Assert.Equal("Ready", result.State);
        Assert.Single(result.Blocks);
        Assert.True(result.Blocks[0].Uncertain);
        Assert.Equal(1, result.Blocks[0].PageNumber);
        Assert.Equal("Nu depășiți 10 unități.", result.Blocks[0].Text);
    }

    [Fact]
    public async Task DemoNeverPretendsToOcrScans()
    {
        var extractor = new LayoutExtractor(
            new HttpClient(),
            new FakeObjectStore([]),
            Options.Create(new ProviderOptions { Demo = true })
        );
        var error = await Assert.ThrowsAsync<ProviderFault>(() =>
            extractor.ExtractAsync(new(Guid.NewGuid(), "key", "application/pdf", null), default)
        );
        Assert.Equal("scan_requires_azure_layout", error.Message);
    }
}
