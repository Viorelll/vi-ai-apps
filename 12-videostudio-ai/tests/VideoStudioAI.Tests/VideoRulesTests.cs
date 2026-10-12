using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Domain.Models;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Tests;

public sealed class VideoRulesTests
{
    [Theory]
    [InlineData("completed", false, "Downloading")]
    [InlineData("completed", true, "Ready")]
    [InlineData("in_progress", false, "InProgress")]
    [InlineData("bogus", false, "NeedsReview")]
    [InlineData("canceled", false, "Canceled")]
    public void ReadyRequiresVerifiedBytes(string status, bool verified, string expected) =>
        Assert.Equal(expected, VideoRules.MapState(status, verified));

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 2)]
    [InlineData(3, 16)]
    [InlineData(5, 60)]
    [InlineData(100, 60)]
    public void PollBackoffIsBounded(int attempt, int seconds) =>
        Assert.Equal(seconds, VideoRules.PollDelay(attempt).TotalSeconds);

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("https://evil.test/file")]
    [InlineData("a?token=x")]
    [InlineData("")]
    public void ProviderIdsCannotConstructArbitraryUrls(string id) =>
        Assert.False(VideoRules.ValidProviderId(id));

    [Fact]
    public void AmbiguousSubmitIsNeverAutomaticallyRetried()
    {
        var j = new GenerationJob { Phase = "Submitting" };
        VideoJobProcessor.HandleFailure(j, new HttpRequestException());
        Assert.Equal("Uncertain", j.State);
        Assert.Equal(0, j.Attempt);
    }

    [Fact]
    public void ThrottleBeforeAcceptanceHasBoundedRetries()
    {
        var j = new GenerationJob { Phase = "Submitting" };
        for (int i = 0; i < 3; i++)
            VideoJobProcessor.HandleFailure(j, new AppException(429, "provider_429", "quota"));
        Assert.Equal("Failed", j.State);
        Assert.Equal(3, j.Attempt);
    }

    [Fact]
    public void AuthenticationFailureDoesNotRetry()
    {
        var j = new GenerationJob { Phase = "Submitting" };
        VideoJobProcessor.HandleFailure(j, new AppException(401, "provider_401", "auth"));
        Assert.Equal("Failed", j.State);
    }

    [Fact]
    public void SceneAndCapabilityValidationRejectInvalidInputs()
    {
        Assert.Throws<AppException>(() =>
            ProjectService.ValidateScenes([new("x", "title", "p"), new("x", "title", "p")])
        );
        Assert.Throws<AppException>(() =>
            ProjectService.ValidateParameters(
                new("Foundry", "sora", [8], ["1280x720"], true),
                9,
                "1280x720"
            )
        );
    }

    [Fact]
    public void UploadChecksMagicBytesAndUtf8()
    {
        Assert.Equal("application/pdf", DocumentService.DetectMime("%PDF-1.7"u8.ToArray()));
        Assert.Equal("text/plain", DocumentService.DetectMime("Brief românesc"u8.ToArray()));
        Assert.Throws<AppException>(() => DocumentService.DetectMime([0, 1, 2, 3]));
    }
}
