using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Database;
using ShopVisionAI.Domain.Entities;
using ShopVisionAI.Services.Clients;
using ShopVisionAI.Services.Documents;
using ShopVisionAI.Services.Errors;
using ShopVisionAI.Services.Matching;
using ShopVisionAI.Services.Searches;
using ShopVisionAI.Services.Workers;

namespace ShopVisionAI.Tests.Integration;

[Collection("database")]
public sealed class WorkflowTests(DatabaseFixture fixture)
{
    private static byte[] Png() =>
        [137, 80, 78, 71, 13, 10, 26, 10, .. Guid.NewGuid().ToByteArray()];

    private sealed class Factory(DatabaseFixture f) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => f.Create();
    }

    private sealed class Handler(bool fail = false) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (request.RequestUri!.AbsolutePath.Contains("grounded"))
            {
                var g = (await request.Content!.ReadFromJsonAsync<GroundedRequest>(ct))!;
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new GroundedResponse(
                            "Candidați, nu SKU confirmat.",
                            g.Candidates.Select(c => new CandidateExplanation(
                                    c.Sku,
                                    c.Reason,
                                    c.SourceIds
                                ))
                                .ToArray(),
                            g.Candidates.SelectMany(c => c.SourceIds).Distinct().ToArray(),
                            0,
                            0,
                            false
                        )
                    ),
                };
            }
            var input = (await request.Content!.ReadFromJsonAsync<VisionRequest>(ct))!;
            if (fail)
                return new(HttpStatusCode.ServiceUnavailable);
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new VisionResponse(
                        "Completed",
                        [
                            new(input.SourceId, "visible", "category", "adapter", null),
                            new(input.SourceId, "visible", "color", "black", null),
                        ],
                        "fake-http",
                        100,
                        40,
                        false
                    )
                ),
            };
        }
    }

    private DurableVisionWorker Worker(bool fail = false) =>
        new(
            new Factory(fixture),
            new DomainAiClient(
                new HttpClient(new Handler(fail)) { BaseAddress = new Uri("http://fake/") }
            ),
            new MatchingService(),
            NullLogger<DurableVisionWorker>.Instance
        );

    [Fact]
    public async Task UploadDeduplicatesAndPreservesContent()
    {
        await using var db = fixture.Create();
        var service = new DocumentService(db, fixture.Store);
        var bytes = Png();
        var first = await service.UploadAsync(
            new MemoryStream(bytes),
            "seed.png",
            bytes.Length,
            default
        );
        var second = await service.UploadAsync(
            new MemoryStream(bytes),
            "copy.png",
            bytes.Length,
            default
        );
        Assert.False(first.Existing);
        Assert.True(second.Existing);
        Assert.Equal(first.Document.Id, second.Document.Id);
        using var stored = await fixture.Store.OpenAsync(first.Document.ObjectKey, default);
        using var content = new MemoryStream();
        await stored.ResponseStream.CopyToAsync(content);
        Assert.Equal(bytes, content.ToArray());
    }

    [Fact]
    public async Task EndToEndPersistsResultAndQuestionsAcrossContexts()
    {
        Guid id;
        await using (var db = fixture.Create())
        {
            var doc = (
                await new DocumentService(db, fixture.Store).UploadAsync(
                    new MemoryStream(Png()),
                    "seed.png",
                    24,
                    default
                )
            ).Document;
            var service = new VisualSearchService(
                db,
                new DomainAiClient(
                    new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
                )
            );
            var search = await service.CreateAsync(new(doc.Id, null), default);
            id = search.Id;
            var request = new AnalyzeRequest(1, Guid.NewGuid());
            var job = await service.AnalyzeAsync(id, request, default);
            var replay = await service.AnalyzeAsync(id, request, default);
            Assert.Equal(job.Id, replay.Id);
        }
        Assert.True(await Worker().RunOnceAsync(default));
        await using (var db = fixture.Create())
        {
            var service = new VisualSearchService(
                db,
                new DomainAiClient(
                    new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
                )
            );
            var search = await service.FindAsync(id, default);
            Assert.Equal("Completed", search.State);
            Assert.Equal(100, search.InputTokens);
            var result = JsonSerializer.Deserialize<SearchResult>(
                search.ResultJson!,
                VisualSearchService.Json
            )!;
            Assert.Equal(2, result.Candidates.Count);
            Assert.All(result.Candidates, c => Assert.Contains("catalog:" + c.Sku, c.SourceIds));
            var req = new QuestionRequest("Care este stocul?", Guid.NewGuid());
            var first = await service.QuestionAsync(id, req, default);
            var second = await service.QuestionAsync(id, req, default);
            Assert.Equal(first.Answer, second.Answer);
            Assert.Single(await db.ConversationMessages.Where(x => x.SearchId == id).ToListAsync());
        }
    }

    [Fact]
    public async Task RevisionConflictRejected()
    {
        await using var db = fixture.Create();
        var d = (
            await new DocumentService(db, fixture.Store).UploadAsync(
                new MemoryStream(Png()),
                "seed.png",
                24,
                default
            )
        ).Document;
        var service = new VisualSearchService(
            db,
            new DomainAiClient(
                new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
            )
        );
        var s = await service.CreateAsync(new(d.Id, null), default);
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            service.AnalyzeAsync(s.Id, new(99, Guid.NewGuid()), default)
        );
        Assert.Equal(409, error.Status);
    }

    [Fact]
    public async Task UniqueSkuEnforcedByPostgres()
    {
        await using var db = fixture.Create();
        db.CatalogProducts.Add(
            new CatalogProduct
            {
                Id = Guid.NewGuid(),
                Sku = "USB-BLK-01",
                CapturedAtUtc = DateTimeOffset.UtcNow,
            }
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ExpiredLeaseReclaimedAndConcurrentWorkersDoNotDuplicate()
    {
        await using var db = fixture.Create();
        var d = (
            await new DocumentService(db, fixture.Store).UploadAsync(
                new MemoryStream(Png()),
                "seed.png",
                24,
                default
            )
        ).Document;
        var service = new VisualSearchService(
            db,
            new DomainAiClient(
                new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
            )
        );
        var s = await service.CreateAsync(new(d.Id, null), default);
        var j = await service.AnalyzeAsync(s.Id, new(1, Guid.NewGuid()), default);
        j.Status = "Running";
        j.LeaseToken = Guid.NewGuid();
        j.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Task.WhenAll(Worker().RunOnceAsync(default), Worker().RunOnceAsync(default));
        db.ChangeTracker.Clear();
        var job = await db.IngestionJobs.SingleAsync(x => x.Id == j.Id);
        Assert.Equal("Completed", job.Status);
        Assert.Equal(1, job.Attempt);
        Assert.Equal("Completed", (await service.FindAsync(s.Id, default)).State);
    }

    [Fact]
    public async Task TransientProviderFailureQueuesRetry()
    {
        await using var db = fixture.Create();
        var d = (
            await new DocumentService(db, fixture.Store).UploadAsync(
                new MemoryStream(Png()),
                "seed.png",
                24,
                default
            )
        ).Document;
        var service = new VisualSearchService(
            db,
            new DomainAiClient(
                new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
            )
        );
        var s = await service.CreateAsync(new(d.Id, null), default);
        var j = await service.AnalyzeAsync(s.Id, new(1, Guid.NewGuid()), default);
        await Worker(true).RunOnceAsync(default);
        db.ChangeTracker.Clear();
        var job = await db.IngestionJobs.SingleAsync(x => x.Id == j.Id);
        Assert.Equal("RetryWaiting", job.Status);
        Assert.True(job.NextAttemptAtUtc > DateTimeOffset.UtcNow);
        job.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await Worker().RunOnceAsync(default);
    }

    [Fact]
    public async Task DeletedImageMakesSearchStaleAndRemovesObject()
    {
        await using var db = fixture.Create();
        var docs = new DocumentService(db, fixture.Store);
        var d = (await docs.UploadAsync(new MemoryStream(Png()), "seed.png", 24, default)).Document;
        var service = new VisualSearchService(
            db,
            new DomainAiClient(
                new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
            )
        );
        var search = await service.CreateAsync(new(d.Id, null), default);
        await docs.DeleteAsync(d.Id, default);
        Assert.Equal("Stale", (await service.FindAsync(search.Id, default)).State);
        await Assert.ThrowsAsync<Amazon.S3.Model.NoSuchKeyException>(() =>
            fixture.Store.OpenAsync(d.ObjectKey, default)
        );
    }

    [Fact]
    public async Task ExpiredFinalAttemptBecomesTerminalFailure()
    {
        await using var db = fixture.Create();
        var d = (
            await new DocumentService(db, fixture.Store).UploadAsync(
                new MemoryStream(Png()),
                "seed.png",
                24,
                default
            )
        ).Document;
        var service = new VisualSearchService(
            db,
            new DomainAiClient(
                new HttpClient(new Handler()) { BaseAddress = new Uri("http://fake/") }
            )
        );
        var search = await service.CreateAsync(new(d.Id, null), default);
        var job = await service.AnalyzeAsync(search.Id, new(1, Guid.NewGuid()), default);
        job.Status = "Running";
        job.Attempt = 3;
        job.LeaseToken = Guid.NewGuid();
        job.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Worker().RunOnceAsync(default);
        Assert.Equal("Failed", (await service.FindAsync(search.Id, default)).State);
        db.ChangeTracker.Clear();
        Assert.Equal("Failed", (await db.IngestionJobs.SingleAsync(x => x.Id == job.Id)).Status);
    }

    [Fact]
    public async Task ConcurrentDatabaseMutationRejectsStaleRevision()
    {
        await using var first = fixture.Create();
        var d = (
            await new DocumentService(first, fixture.Store).UploadAsync(
                new MemoryStream(Png()),
                "seed.png",
                24,
                default
            )
        ).Document;
        var a = new VisualSearch { ImageDocumentId = d.Id };
        first.VisualSearches.Add(a);
        await first.SaveChangesAsync();
        await using var second = fixture.Create();
        var b = await second.VisualSearches.SingleAsync(x => x.Id == a.Id);
        a.Revision++;
        a.Question = "first";
        await first.SaveChangesAsync();
        b.Revision++;
        b.Question = "stale";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
