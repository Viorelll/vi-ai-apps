using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Infrastructure.Media;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Models;
using VideoStudioAI.Tests.Fixtures;

namespace VideoStudioAI.Tests;

[Collection("Postgres")]
public sealed class RecoveryTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task CrashDuringSubmitBecomesUncertainWithoutAnotherExternalWrite()
    {
        await using var db = fixture.Create();
        await db.Jobs.ExecuteDeleteAsync();
        var p = new VideoProject { Brief = "crash recovery" };
        db.Add(p);
        var j = new GenerationJob
        {
            ProjectId = p.Id,
            RequestId = Guid.NewGuid(),
            Kind = "Video",
            State = "InProgress",
            Phase = "Submitting",
            InputJson = JsonSerializer.Serialize(
                new VideoSubmit("coffee", 8, "1280x720", "demo-sora"),
                ProjectService.Json
            ),
        };
        db.Add(j);
        await db.SaveChangesAsync();
        var factory = new ContextFactory(fixture);
        var claimed = await new JobLeaseService(factory).ClaimVideoAsync(default);
        Assert.NotNull(claimed);
        var ai = new ForbiddenGateway();
        await new VideoJobProcessor(
            factory,
            ai,
            new ForbiddenStore(),
            new MediaValidator(),
            NullLogger<VideoJobProcessor>.Instance
        ).ProcessAsync(claimed, default);
        db.ChangeTracker.Clear();
        var result = await db.Jobs.SingleAsync(x => x.Id == j.Id);
        Assert.Equal("Uncertain", result.State);
        Assert.Null(result.ProviderId);
        Assert.Equal(0, ai.Calls);
        Assert.Null(await new JobLeaseService(factory).ClaimVideoAsync(default));
    }

    [Fact]
    public async Task UncertainRetryRequiresCostAcknowledgmentAndCreatesNewOperation()
    {
        await using var db = fixture.Create();
        var p = new VideoProject { Brief = "uncertain", Revision = 2 };
        db.Add(p);
        var j = new GenerationJob
        {
            ProjectId = p.Id,
            RequestId = Guid.NewGuid(),
            Kind = "Video",
            State = "Uncertain",
            InputJson = JsonSerializer.Serialize(
                new VideoSubmit("coffee", 8, "1280x720", "demo-sora"),
                ProjectService.Json
            ),
        };
        db.Add(j);
        await db.SaveChangesAsync();
        var service = new ProjectService(db, new ForbiddenGateway());
        var error = await Assert.ThrowsAsync<AppException>(() =>
            service.RetryAsync(j.Id, Guid.NewGuid(), 2, false, default)
        );
        Assert.Equal("cost_confirmation", error.Code);
        var retry = await service.RetryAsync(j.Id, Guid.NewGuid(), 2, true, default);
        Assert.NotEqual(j.Id, retry.Id);
        Assert.Equal("Uncertain", j.State);
        Assert.Equal("Queued", retry.State);
        retry.State = "Failed";
        await db.SaveChangesAsync();
    }
}
