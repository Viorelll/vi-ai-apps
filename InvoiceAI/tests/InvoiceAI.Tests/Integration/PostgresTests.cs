using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InvoiceAI.Database.Repositories;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Services.Exceptions;
using InvoiceAI.Services.Services;
using InvoiceAI.Tests.Fixtures;
namespace InvoiceAI.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class PostgresTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private async Task<Invoice> SeedAsync()
    {
        var d = new Document { Sha256 = Guid.NewGuid().ToString("N"), FileName = "seed.pdf", ObjectKey = "seed", ContentType = "application/pdf" };
        var docs = new DocumentRepository(fixture.Factory);
        await docs.AddAsync(d, default);
        return await new InvoiceRepository(fixture.Factory).CreateAsync(d.Id, "invoice", default);
    }
    [Fact]
    public async Task AnalysisIsIdempotentAndLeaseIsExclusive()
    {
        var i = await SeedAsync();
        var repo = new InvoiceRepository(fixture.Factory);
        var ids = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => repo.AnalyzeAsync(i.Id, default)));
        Assert.Single(ids.Distinct());
        var jobs = new JobRepository(fixture.Factory);
        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => jobs.ClaimAsync(default)));
        var job = Assert.Single(claims, x => x?.Id == ids[0])!;
        Assert.NotNull(job.LeaseToken);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        await db.IngestionJobs.Where(x => x.Id == job.Id).ExecuteUpdateAsync(x => x.SetProperty(j => j.LeaseUntilUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        var reclaimed = await jobs.ClaimAsync(default);
        Assert.NotNull(reclaimed);
        Assert.NotEqual(job.LeaseToken, reclaimed.LeaseToken);
        Assert.Equal("LEASE_LOST", (await Assert.ThrowsAsync<AppException>(() => jobs.SchedulePollAsync(job, "operation", 1, default))).Code);
        await jobs.FailAsync(reclaimed, "TEST_DONE", false, 0, default);
    }
    [Fact]
    public async Task MismatchCorrectionExportAndConcurrency()
    {
        var i = await SeedAsync();
        var repo = new InvoiceRepository(fixture.Factory);
        await repo.AnalyzeAsync(i.Id, default);
        var jobs = new JobRepository(fixture.Factory);
        var j = await jobs.ClaimAsync(default);
        Assert.Equal(i.Id, j!.TargetId);
        await jobs.ActivateAsync(j, InvoiceFixture.Extraction(i.DocumentId), "raw", default);
        j = await jobs.ClaimAsync(default);
        await jobs.CompleteAsync(j!, new("Seed", null, [], "fake", 10, 5), default);
        var service = new InvoiceService(repo);
        var result = await service.GetAsync(i.Id, default);
        Assert.Equal("NeedsReview", result.State);
        Assert.Contains(result.Issues, x => x.Code == "TOTAL_MISMATCH");
        Assert.Equal("EXPORT_BLOCKED", (await Assert.ThrowsAsync<AppException>(() => service.ExportAsync(i.Id, default))).Code);
        var revision = result.Revision;
        result = await service.CorrectAsync(i.Id, new(revision, new() { { "total", JsonSerializer.SerializeToElement(120) } }), default);
        Assert.Equal("Validated", result.State);
        Assert.Equal(119, result.Fields.Single(x => x.Path == "total").Value.GetDecimal());
        Assert.Equal(120, result.Corrections["total"].GetDecimal());
        Assert.Equal("REVISION_CONFLICT", (await Assert.ThrowsAsync<AppException>(() => service.CorrectAsync(i.Id, new(revision, new() { { "total", JsonSerializer.SerializeToElement(121) } }), default))).Code);
        var snapshots = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => service.ExportAsync(i.Id, default)));
        Assert.Single(snapshots.Select(x => x.Id).Distinct());
        using var payload = JsonDocument.Parse(snapshots[0].PayloadJson);
        Assert.Equal(120, payload.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal());
        Assert.Equal(120, payload.RootElement.GetProperty("corrections").GetProperty("total").GetDecimal());
        await service.CorrectAsync(i.Id, new(result.Revision, new() { { "total", JsonSerializer.SerializeToElement(119) } }), default);
        await Assert.ThrowsAsync<AppException>(() => service.ExportAsync(i.Id, default));
    }
    [Fact]
    public async Task DeletionHidesSourceAndCancelsWorker()
    {
        var i = await SeedAsync();
        var repo = new InvoiceRepository(fixture.Factory);
        await repo.AnalyzeAsync(i.Id, default);
        var jobs = new JobRepository(fixture.Factory);
        var j = await jobs.ClaimAsync(default);
        var docs = new DocumentRepository(fixture.Factory);
        var cleanup = await docs.DeleteAsync(i.DocumentId, default);
        await Assert.ThrowsAsync<AppException>(() => repo.GetAsync(i.Id, default));
        await Assert.ThrowsAsync<AppException>(() => docs.GetAsync(i.DocumentId, default));
        await Assert.ThrowsAsync<AppException>(() => jobs.ActivateAsync(j!, InvoiceFixture.Extraction(i.DocumentId), "raw", default));
        var c = await jobs.ClaimAsync(default);
        Assert.Equal(cleanup, c!.Id);
        await jobs.FinishCleanupAsync(c, default);
    }
    [Fact]
    public async Task RetryIsBoundedAndDurable()
    {
        var i = await SeedAsync();
        var repo = new InvoiceRepository(fixture.Factory);
        await repo.AnalyzeAsync(i.Id, default);
        var jobs = new JobRepository(fixture.Factory);
        for (var n = 0; n < 3; n++)
        {
            var j = await jobs.ClaimAsync(default);
            await jobs.FailAsync(j!, "PROVIDER_UNAVAILABLE", true, 0, default);
        }
        Assert.Equal("Failed", (await repo.GetAsync(i.Id, default)).State);
        await new DocumentRepository(fixture.Factory).RetryAsync(i.DocumentId, default);
        Assert.Equal("Extracting", (await repo.GetAsync(i.Id, default)).State);
        var retry = await jobs.ClaimAsync(default);
        Assert.Equal(0, retry!.Attempt);
        await jobs.FailAsync(retry, "TEST_DONE", false, 0, default);
    }
    [Fact]
    public async Task HashUniqueConstraintIsRealPostgres()
    {
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var hash = Guid.NewGuid().ToString("N");
        db.Documents.Add(new()
        {
            Sha256 = hash
        });
        await db.SaveChangesAsync();
        db.Documents.Add(new()
        {
            Sha256 = hash
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task ExplanationRetryRestoresSourceReadiness()
    {
        var i = await SeedAsync();
        var repo = new InvoiceRepository(fixture.Factory);
        var jobs = new JobRepository(fixture.Factory);
        await repo.AnalyzeAsync(i.Id, default);
        var extraction = await jobs.ClaimAsync(default);
        await jobs.ActivateAsync(extraction!, InvoiceFixture.Extraction(i.DocumentId), "raw", default);
        var explain = await jobs.ClaimAsync(default);
        await jobs.FailAsync(explain!, "PROVIDER_AUTH_FAILED", false, 0, default, 11, 5);
        var failed = await repo.GetAsync(i.Id, default);
        Assert.Equal(11, failed.InputTokens);
        await new DocumentRepository(fixture.Factory).RetryAsync(i.DocumentId, default);
        var resumed = await jobs.ClaimAsync(default);
        Assert.Equal("Explain", resumed!.Phase);
        await jobs.CompleteAsync(resumed, new("Seed", null, [], "fake", 10, 3), default);
        var completed = await repo.GetAsync(i.Id, default);
        Assert.Equal("Ready", completed.Document.Status);
        Assert.Equal(21, completed.InputTokens);
        Assert.Equal(8, completed.OutputTokens);
        var correction = await new InvoiceService(repo).CorrectAsync(i.Id, new(completed.Revision, new() { { "total", JsonSerializer.SerializeToElement(120) } }), default);
        Assert.Equal("Validated", correction.State);
    }
}
