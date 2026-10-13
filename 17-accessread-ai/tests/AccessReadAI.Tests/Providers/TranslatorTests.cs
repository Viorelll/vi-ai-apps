using System.Net;
using AccessReadAI.Ai.Models;
using AccessReadAI.Ai.Providers;
using Microsoft.Extensions.Options;

namespace AccessReadAI.Tests.Providers;

public sealed class TranslatorTests
{
    [Fact]
    public async Task UsesDedicatedEndpointAndKeepsSourceSeparate()
    {
        var client = new TranslatorClient(
            new HttpClient(
                new FakeHttpHandler(async request =>
                {
                    Assert.Equal("api.cognitive.microsofttranslator.com", request.RequestUri!.Host);
                    Assert.Contains("to=en", request.RequestUri.Query);
                    Assert.Contains(
                        "Nu depășiți",
                        System
                            .Text.Json.JsonDocument.Parse(
                                await request.Content!.ReadAsStringAsync()
                            )
                            .RootElement[0]
                            .GetProperty("Text")
                            .GetString()
                    );
                    Assert.True(request.Headers.Contains("Ocp-Apim-Subscription-Region"));
                    return new(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """[{"translations":[{"text":"Do not exceed 10 units.","to":"en"}]}]"""
                        ),
                    };
                })
            ),
            Options.Create(new ProviderOptions())
        );
        Assert.Equal(
            "Do not exceed 10 units.",
            await client.TranslateAsync("Nu depășiți 10 unități.", "ro", "en", default)
        );
    }
}
