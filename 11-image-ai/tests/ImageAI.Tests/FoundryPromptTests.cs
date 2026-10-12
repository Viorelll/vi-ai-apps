using System.Net;
using System.Net.Http.Json;
using ImageAI.Ai.Options;
using ImageAI.Ai.Providers;
using ImageAI.Contracts.Dto;
using ImageAI.Tests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace ImageAI.Tests;

public sealed class FoundryPromptTests
{
    [Fact]
    public async Task PreparesPromptThroughResponsesWithBoundedOutput()
    {
        var handler = new FakeHttpHandler(async req =>
        {
            Assert.Equal("/openai/v1/responses", req.RequestUri!.AbsolutePath);
            var body = await req.Content!.ReadAsStringAsync();
            Assert.Contains("max_output_tokens", body);
            Assert.Contains("input_image", body);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new
                    {
                        status = "completed",
                        output = new[]
                        {
                            new
                            {
                                type = "message",
                                content = new[]
                                {
                                    new
                                    {
                                        type = "output_text",
                                        text = "A clean product image with warm light",
                                    },
                                },
                            },
                        },
                        usage = new { input_tokens = 8, output_tokens = 9 },
                    }
                ),
            };
        });
        var service = new FoundryPromptService(
            new HttpClient(handler),
            Options.Create(
                new ImagesOptions
                {
                    Endpoint = "https://example.openai.azure.com/",
                    ApiKey = "dummy",
                    ChatDeployment = "chat-test",
                }
            )
        );
        var result = await service.PrepareAsync(
            new GatewayRequest(
                Guid.NewGuid(),
                "A product photograph",
                "1024x1024",
                "medium",
                Convert.ToBase64String(ImageFixture.Png()),
                null
            ),
            default
        );
        Assert.Equal("A clean product image with warm light", result.Prompt);
        Assert.Contains("output_tokens", result.UsageJson!);
    }

    [Fact]
    public async Task RejectsIncompleteResponses()
    {
        var handler = new FakeHttpHandler(_ =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { status = "incomplete" }),
                }
            )
        );
        var service = new FoundryPromptService(
            new HttpClient(handler),
            Options.Create(
                new ImagesOptions
                {
                    Endpoint = "https://example.openai.azure.com/",
                    ChatDeployment = "chat-test",
                }
            )
        );
        var e = await Assert.ThrowsAsync<ProviderException>(() =>
            service.PrepareAsync(
                new GatewayRequest(
                    Guid.NewGuid(),
                    "Product image",
                    "1024x1024",
                    "medium",
                    null,
                    null
                ),
                default
            )
        );
        Assert.Equal("prompt_response_incomplete", e.Message);
    }
}
