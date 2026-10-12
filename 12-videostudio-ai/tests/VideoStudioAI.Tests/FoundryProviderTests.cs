using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VideoStudioAI.Ai.Models;
using VideoStudioAI.Ai.Providers;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Tests.Fixtures;

namespace VideoStudioAI.Tests;

public sealed class FoundryProviderTests
{
    private static FoundryProvider Provider(RecordingHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(
                new FoundryOptions
                {
                    OpenAiEndpoint = "https://example.openai.azure.com/openai/v1/",
                    ApiKey = "test-key",
                    VideoDeployment = "sora-2",
                    ChatDeployment = "gpt-4.1-mini",
                }
            )
        );

    [Fact]
    public async Task SubmitUsesMultipartV1AndNoLegacyJobs()
    {
        using var h = new RecordingHandler(async r =>
        {
            Assert.Equal("/openai/v1/videos", r.RequestUri!.AbsolutePath);
            Assert.Equal("test-key", r.Headers.GetValues("api-key").Single());
            Assert.StartsWith("multipart/form-data", r.Content!.Headers.ContentType!.ToString());
            var body = await r.Content.ReadAsStringAsync();
            Assert.Contains("sora-2", body);
            Assert.Contains("1280x720", body);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"video_123\",\"status\":\"queued\"}"),
            };
        });
        Assert.Equal(
            "video_123",
            (await Provider(h).SubmitAsync(new("coffee", 8, "1280x720", "sora-2"), default)).Id
        );
    }

    [Fact]
    public async Task RemixUsesSourceIdAndPromptOnly()
    {
        using var h = new RecordingHandler(async r =>
        {
            Assert.Equal("/openai/v1/videos/video_source/remix", r.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
            Assert.Equal("warm palette", body.RootElement.GetProperty("prompt").GetString());
            Assert.False(body.RootElement.TryGetProperty("model", out _));
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"video_remix\",\"status\":\"queued\"}"),
            };
        });
        await Provider(h)
            .SubmitAsync(new("warm palette", 8, "1280x720", "sora-2", "video_source"), default);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(401)]
    public async Task HttpFailuresPreserveStatusAndDoNotRetry(int status)
    {
        int calls = 0;
        using var h = new RecordingHandler(r =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        });
        var error = await Assert.ThrowsAsync<GatewayException>(() =>
            Provider(h).PollAsync("video_123", default)
        );
        Assert.Equal(status, error.Status);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void IncompleteAndRefusedResponsesFailValidation()
    {
        using var incomplete = JsonDocument.Parse("{\"status\":\"incomplete\",\"output\":[]}");
        Assert.Throws<GatewayException>(() => FoundryProvider.OutputText(incomplete.RootElement));
        using var refusal = JsonDocument.Parse(
            "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"No\"}]}]}"
        );
        Assert.Throws<GatewayException>(() => FoundryProvider.OutputText(refusal.RootElement));
    }

    [Fact]
    public void ExtractsOnlyMessageOutputText()
    {
        using var doc = JsonDocument.Parse(
            "{\"status\":\"completed\",\"output\":[{\"type\":\"reasoning\"},{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"ok\"}]}]}"
        );
        Assert.Equal("ok", FoundryProvider.OutputText(doc.RootElement));
    }

    [Fact]
    public async Task ContentDownloadIsBoundedAndUsesValidatedId()
    {
        using var h = new RecordingHandler(r =>
        {
            Assert.Equal("/openai/v1/videos/video_123/content", r.RequestUri!.AbsolutePath);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("fixture")),
                }
            );
        });
        var path = await Provider(h).DownloadAsync("video_123", default);
        try
        {
            Assert.Equal("fixture", await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
        await Assert.ThrowsAsync<GatewayException>(() =>
            Provider(h).DownloadAsync("../etc/passwd", default)
        );
    }
}
