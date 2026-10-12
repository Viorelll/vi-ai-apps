using Microsoft.EntityFrameworkCore;
using ProcureAI.Ai.Providers;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Services;
using ProcureAI.Database.Persistence;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Entities;
using ProcureAI.Domain.Models;

namespace ProcureAI.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PostgresWorkflowTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AtomicClaimReclaimsExpiredLeaseAndRejectsOldOwner()
    {
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var job = new DurableJob { Kind = "Test" }; db.Jobs.Add(job); await db.SaveChangesAsync();
        var store = new JobStore(fixture.Factory);
        var claims = await Task.WhenAll(store.ClaimAsync(default), store.ClaimAsync(default));
        var first = Assert.Single(claims, x => x is not null)!;
        await db.Jobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        var second = await store.ClaimAsync(default); Assert.NotNull(second); Assert.NotEqual(first.LeaseToken, second.LeaseToken);
        await store.ReleaseAsync(first, "Complete", TimeSpan.Zero, null, null, default);
        await using var check = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal("Running", (await check.Jobs.FindAsync(job.Id))!.State);
        await store.ReleaseAsync(second, "Complete", TimeSpan.Zero, null, null, default);
    }
    [Fact]
    public async Task UniqueHashAndRevisionConflictsDoNotDuplicateJobsOrLinks()
    {
        var repository = new ProcurementRepository(fixture.Factory);
        var a = Document("unique", "{}"); var b = Document("unique", "{}");
        var docs = await Task.WhenAll(repository.AddDocumentAsync(a, default), repository.AddDocumentAsync(b, default));
        Assert.Equal(docs[0].Id, docs[1].Id);
        var run = await repository.CreateAsync(new CreateRunRequest("10 units", new("EUR", 10, "units", 10)), default);
        async Task<bool> Attach()
        { try { await repository.AttachAsync(run.Id, new(docs[0].Id, "offer", 0), default); return true; } catch (AppException e) when (e.Status == 409) { return false; } }
        Assert.Single(await Task.WhenAll(Attach(), Attach()), x => x);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(1, await db.RunDocuments.CountAsync(x => x.RunId == run.Id));
        Assert.Equal(1, await db.Jobs.CountAsync(x => x.DocumentId == docs[0].Id));
        await db.Jobs.Where(x => x.DocumentId == docs[0].Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Complete"));
    }
    [Fact]
    public async Task FourStagesResumeFromCheckpointAndDeletionMarksResultStale()
    {
        var repository = new ProcurementRepository(fixture.Factory); var jobs = new JobStore(fixture.Factory);
        var run = await repository.CreateAsync(new CreateRunRequest("10 units", new("EUR", 10, "units", 10)), default);
        long revision = 0;
        foreach (var (supplier, net, shipping, days) in new[] { ("A", 1000, 50, 7), ("B", 980, 120, 9) })
        {
            var text = JsonDefaults.Serialize(new { supplierLabel = supplier, currency = "EUR", quantity = 10, unit = "units", net, shipping, leadDays = days, meetsRequirements = true });
            var document = await repository.AddDocumentAsync(Document(Guid.NewGuid().ToString(), text), default);
            var job = await WaitClaimAsync(jobs); Assert.NotNull(job);
            await repository.CompleteDocumentAsync(job, text, default);
            await repository.AttachAsync(run.Id, new(document.Id, "offer", revision++), default);
        }
        await repository.StartAsync(run.Id, revision, false, default);
        await Assert.ThrowsAsync<AppException>(() => repository.StartAsync(run.Id, revision, false, default));
        var ai = new DemoGateway();
        for (var i = 0; i < 4; i++)
        {
            // New processor and contexts on every step simulate loss of the previous in-process state.
            var processor = new WorkflowProcessor(new ProcurementRepository(fixture.Factory), ai, new OfferScorer(), new EvidenceValidator());
            var job = await WaitClaimAsync(jobs); Assert.NotNull(job);
            await processor.ProcessAsync(job, default);
            var current = await repository.GetRunAsync(run.Id, default); Assert.Equal(i + 1, current!.Steps.Count);
        }
        var result = await repository.GetRunAsync(run.Id, default);
        Assert.Equal("Completed", result!.State); Assert.Equal(1050, result.Scores[0].Cost); Assert.Equal(.79m, result.Scores[0].Score);
        Assert.Equal(new[] { "needs", "suppliers", "compare", "recommend" }, result.Steps.Select(x => x.Role));
        Assert.Equal(result.Scores[0].Id, result.Recommendation!.RecommendedOfferId);
        var deletedDocumentId = result.Documents[0].Id;
        await repository.DeleteDocumentAsync(deletedDocumentId, default);
        result = await repository.GetRunAsync(run.Id, default); Assert.True(result!.Stale);
        Assert.DoesNotContain(result.Sources, x => x.DocumentId == deletedDocumentId);
        var cleanup = await jobs.ClaimAsync(default); Assert.NotNull(cleanup);
        await jobs.ReleaseAsync(cleanup, "Complete", TimeSpan.Zero, null, null, default);
    }
    private static async Task<DurableJob> WaitClaimAsync(JobStore jobs)
    {
        for (var i = 0; i < 30; i++)
        {
            var job = await jobs.ClaimAsync(default);
            if (job is not null) return job;
            await Task.Delay(100);
        }
        throw new InvalidOperationException("Queued job did not become available.");
    }

    private static Document Document(string hash, string text) => new()
    { FileName = "offer.json", ContentType = "application/json", Sha256 = hash, ObjectKey = "fixture/" + hash, SizeBytes = text.Length };
}
