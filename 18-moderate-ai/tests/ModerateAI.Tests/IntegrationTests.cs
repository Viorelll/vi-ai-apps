using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Models;
using ModerateAI.Services;
using SkiaSharp;
using Xunit;

namespace ModerateAI.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class IntegrationTests(IntegrationFixture fixture)
{
    private ModerationService Service => new(fixture.Factory);

    private DurableWorker Worker(TestAnalysisGateway gateway) =>
        new(fixture.Factory, gateway, fixture.Store, NullLogger<DurableWorker>.Instance);

    private async Task<Guid> Create(string text) =>
        (await Service.CreateAsync(new(Guid.NewGuid(), text, null), default)).Id;

    [Fact]
    public async Task RequestDedupAndHashConflictAreEnforced()
    {
        var request = new CreateItemRequest(Guid.NewGuid(), "allow", null);
        var results = await Task.WhenAll(
            Service.CreateAsync(request, default),
            Service.CreateAsync(request, default)
        );
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(
            409,
            (
                await Assert.ThrowsAsync<DomainException>(() =>
                    Service.CreateAsync(request with { Text = "changed" }, default)
                )
            ).Status
        );
    }

    [Theory]
    [InlineData("allow", "Allowed")]
    [InlineData("review", "Review")]
    [InlineData("block", "Blocked")]
    public async Task ScoresAndPolicyPersistOnRealDatabaseAndS3(string text, string state)
    {
        Guid id = await Create(text);
        await Service.AnalyzeAsync(id, default);
        var gateway = new TestAnalysisGateway();
        Assert.True(await Worker(gateway).RunOneAsync(default));
        var result = await Service.GetAsync(id, false, default);
        Assert.Equal(state, result.State);
        Assert.Null(result.Text);
        await using var db = fixture.Factory.CreateDbContext();
        var analysis = await db.SafetyAnalyses.SingleAsync(x => x.ItemId == id);
        Assert.Equal(4, await db.CategoryFindings.CountAsync(x => x.AnalysisId == analysis.Id));
        Assert.Contains(
            "integration",
            System.Text.Encoding.UTF8.GetString(
                await fixture.Store.ReadAsync(analysis.RawObjectKey, default)
            )
        );
        Assert.False(await Worker(gateway).RunOneAsync(default));
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task ConcurrentReviewHasOneWinnerAndPreservesRawPolicy()
    {
        Guid id = await Create("review");
        await Service.AnalyzeAsync(id, default);
        await Worker(new()).RunOneAsync(default);
        var initial = await Service.GetAsync(id, false, default);
        async Task<int> Review(string decision)
        {
            try
            {
                await Service.ReviewAsync(
                    id,
                    new(initial.Revision, decision, "synthetic override"),
                    default
                );
                return 200;
            }
            catch (DomainException e)
            {
                return e.Status;
            }
        }
        var outcomes = await Task.WhenAll(Review("allow"), Review("block"));
        Assert.Equal(new[] { 200, 409 }, outcomes.Order().ToArray());
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(
            "review",
            (await db.PolicyDecisions.SingleAsync(x => x.ItemId == id)).Decision
        );
        Assert.Equal(1, await db.ReviewDecisions.CountAsync(x => x.ItemId == id));
    }

    [Fact]
    public async Task ExpiredLeaseIsReclaimedAfterRestart()
    {
        Guid id = await Create("allow");
        Guid job = await Service.AnalyzeAsync(id, default);
        await using (var db = fixture.Factory.CreateDbContext())
        {
            await db
                .IngestionJobs.Where(x => x.Id == job)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.Status, "Running")
                        .SetProperty(x => x.Attempt, 1)
                        .SetProperty(x => x.LeaseToken, Guid.NewGuid())
                        .SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(-1))
                );
        }
        await Worker(new()).RunOneAsync(default);
        Assert.Equal("Allowed", (await Service.GetAsync(id, false, default)).State);
    }

    [Fact]
    public async Task TransientRetriesStopAtThreeAndNeverAllow()
    {
        Guid id = await Create("allow");
        Guid job = await Service.AnalyzeAsync(id, default);
        var gateway = new TestAnalysisGateway
        {
            Failure = new ProviderException("provider_http_503", true),
        };
        for (int i = 0; i < 3; i++)
        {
            Assert.True(await Worker(gateway).RunOneAsync(default));
            await using var db = fixture.Factory.CreateDbContext();
            await db
                .IngestionJobs.Where(x => x.Id == job)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1))
                );
        }
        var result = await Service.GetAsync(id, false, default);
        Assert.Equal("Failed", result.State);
        Assert.Null(result.Policy);
        Assert.Equal(3, gateway.Calls);
    }

    [Fact]
    public async Task ImageDedupDownloadAndDurableDeleteInvalidateResult()
    {
        using var image = new SKBitmap(80, 80);
        image.SetPixel(1, 1, new SKColor((byte)Random.Shared.Next(255), 0, 0));
        using var bytes = new MemoryStream();
        using var encoded = SKImage.FromBitmap(image).Encode(SKEncodedImageFormat.Png, 100);
        encoded.SaveTo(bytes);
        byte[] data = bytes.ToArray();
        var docs = new DocumentService(fixture.Factory, fixture.Store);
        using var a = new MemoryStream(data);
        var first = await docs.UploadAsync(a, "sample.png", default);
        using var b = new MemoryStream(data);
        var second = await docs.UploadAsync(b, "sample.png", default);
        Assert.True(second.Existing);
        Assert.Equal(first.Document.Id, second.Document.Id);
        Assert.Equal(data, await fixture.Store.ReadAsync(first.Document.ObjectKey, default));
        Guid id = (
            await Service.CreateAsync(new(Guid.NewGuid(), "review", first.Document.Id), default)
        ).Id;
        await Service.AnalyzeAsync(id, default);
        await Worker(new()).RunOneAsync(default);
        var initial = await Service.GetAsync(id, false, default);
        await docs.DeleteAsync(first.Document.Id, default);
        Assert.True((await Service.GetAsync(id, false, default)).Stale);
        await Assert.ThrowsAsync<DomainException>(() =>
            Service.ReviewAsync(id, new(initial.Revision, "allow", null), default)
        );
        Assert.True(await Worker(new()).RunOneAsync(default));
        await Assert.ThrowsAsync<DomainException>(() => docs.GetAsync(first.Document.Id, default));
        using var reupload = new MemoryStream(data);
        var restored = await docs.UploadAsync(reupload, "sample.png", default);
        Assert.False(restored.Existing);
        Assert.NotEqual(first.Document.Id, restored.Document.Id);
        Assert.True((await Service.GetAsync(id, false, default)).Stale);
    }

    [Fact]
    public async Task ConcurrentWorkersDoNotDuplicateResults()
    {
        Guid a = await Create("allow"),
            b = await Create("block");
        await Service.AnalyzeAsync(a, default);
        await Service.AnalyzeAsync(b, default);
        await Task.WhenAll(Worker(new()).RunOneAsync(default), Worker(new()).RunOneAsync(default));
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(2, await db.SafetyAnalyses.CountAsync(x => x.ItemId == a || x.ItemId == b));
    }
}
