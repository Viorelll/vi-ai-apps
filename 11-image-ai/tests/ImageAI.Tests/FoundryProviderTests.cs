using System.Net;
using System.Net.Http.Json;
using ImageAI.Ai.Options;
using ImageAI.Ai.Providers;
using ImageAI.Contracts.Dto;
using ImageAI.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace ImageAI.Tests;

public sealed class FoundryProviderTests
{
    private static ImagesOptions Config =>
        new()
        {
            Endpoint = "https://example.openai.azure.com/",
            ApiKey = "dummy-test-key",
            Deployment = "image-test",
        };
    private static GatewayRequest Input =>
        new(Guid.NewGuid(), "A studio product photo", "1024x1024", "medium", null, null);

    private static FoundryImageProvider Provider(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler,
        ImagesOptions? options = null
    )
    {
        var http = new HttpClient(new FakeHttpHandler(handler));
        var configured = Options.Create(options ?? Config);
        return new(http, configured, new FoundryPromptService(http, configured));
    }

    [Fact]
    public async Task SerializesGenerationAndKeepsUsageUnknown()
    {
        var provider = Provider(async r =>
        {
            Assert.Equal(
                "/openai/deployments/image-test/images/generations",
                r.RequestUri!.AbsolutePath
            );
            Assert.Equal("dummy-test-key", r.Headers.GetValues("api-key").Single());
            var body = await r.Content!.ReadAsStringAsync();
            Assert.Contains("output_format", body);
            Assert.Contains("Do not draw banner text", body);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new
                    {
                        data = new[]
                        {
                            new { b64_json = Convert.ToBase64String(ImageFixture.Png(1024, 1024)) },
                        },
                    }
                ),
            };
        });
        var result = await provider.GenerateAsync(Input, default);
        Assert.Null(result.UsageJson);
        Assert.Equal("image-test", result.Deployment);
    }

    [Fact]
    public async Task UsesMultipartForEdits()
    {
        var provider = Provider(async r =>
        {
            Assert.EndsWith("/images/edits", r.RequestUri!.AbsolutePath);
            Assert.IsType<MultipartFormDataContent>(r.Content);
            var body = await r.Content!.ReadAsStringAsync();
            Assert.Contains("mask.png", body);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new
                    {
                        data = new[]
                        {
                            new { b64_json = Convert.ToBase64String(ImageFixture.Png(1024, 1024)) },
                        },
                        usage = new { output_tokens = 4 },
                    }
                ),
            };
        });
        var result = await provider.GenerateAsync(
            Input with
            {
                ImageBase64 = Convert.ToBase64String(ImageFixture.Png(1024, 1024)),
                MaskBase64 = Convert.ToBase64String(ImageFixture.Png(1024, 1024, true)),
            },
            default
        );
        Assert.Contains("output_tokens", result.UsageJson!);
    }

    [Theory]
    [InlineData(429, "provider_rate_limited", 429)]
    [InlineData(401, "provider_auth_failed", 400)]
    [InlineData(503, "provider_operation_uncertain", 502)]
    public async Task MapsProviderErrorsWithoutLeakingBody(int status, string code, int mapped)
    {
        var provider = Provider(_ =>
            Task.FromResult(
                new HttpResponseMessage((HttpStatusCode)status)
                {
                    Content = new StringContent("SECRET_PROVIDER_BODY"),
                }
            )
        );
        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.GenerateAsync(Input, default)
        );
        Assert.Equal(code, error.Message);
        Assert.Equal(mapped, error.Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":[]}")]
    [InlineData("{\"data\":[{\"b64_json\":\"bad-data\"}]}")]
    public async Task RejectsMalformedSuccess(string json)
    {
        var provider = Provider(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
            )
        );
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.GenerateAsync(Input, default)
        );
        Assert.Equal("provider_invalid_response", e.Message);
    }

    [Fact]
    public async Task RejectsUnconfiguredProviderWithoutNetwork()
    {
        var provider = Provider(_ => throw new Exception("should not call"), new());
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.GenerateAsync(Input, default)
        );
        Assert.Equal("provider_not_configured", e.Message);
    }

    [Fact]
    public async Task MapsSafetyBlockWithoutLeakingPolicyBody()
    {
        var provider = Provider(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"code\":\"content_filter\"}}"),
                }
            )
        );
        var error = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.GenerateAsync(Input, default)
        );
        Assert.Equal("provider_safety_blocked", error.Message);
    }

    [Fact]
    public async Task RejectsWrongOutputDimensions()
    {
        var provider = Provider(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new
                        {
                            data = new[]
                            {
                                new { b64_json = Convert.ToBase64String(ImageFixture.Png()) },
                            },
                        }
                    ),
                }
            )
        );
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            provider.GenerateAsync(Input, default)
        );
        Assert.Equal(502, e.Status);
    }
}
