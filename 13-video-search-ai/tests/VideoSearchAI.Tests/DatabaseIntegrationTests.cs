using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Domain.Entities;
using VideoSearchAI.Infrastructure.Ai;
using VideoSearchAI.Services.Options;
using VideoSearchAI.Services.Services;
using VideoSearchAI.Services.Workers;

namespace VideoSearchAI.Tests;

public sealed class DatabaseIntegrationTests(IntegrationEnvironment env)
    : IClassFixture<IntegrationEnvironment>
{
    [IntegrationFact]
    public async Task MigrationsAndUniqueSegmentConstraintUseRealPostgres()
    {
        await env.InitializeAsync();
        await using var db = await env.Factory.CreateDbContextAsync();
        var d = new Document { Sha256 = "a" + Guid.NewGuid().ToString("N").PadRight(63, 'a') };
        var v = new VideoEntry { DocumentId = d.Id, Title = "course" };
        db.Add(d);
        db.Add(v);
        db.Add(
            new VideoSegment
            {
                Id = Guid.NewGuid(),
                VideoId = v.Id,
                Generation = 1,
                Ordinal = 0,
            }
        );
        await db.SaveChangesAsync();
        db.Add(
            new VideoSegment
            {
                Id = Guid.NewGuid(),
                VideoId = v.Id,
                Generation = 1,
                Ordinal = 0,
            }
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(
            "InitialSchema",
            string.Join(',', await db.Database.GetAppliedMigrationsAsync())
        );
    }

    [IntegrationFact]
    public async Task IndexRequestsEnforceRevisionAndQueueOnce()
    {
        await env.InitializeAsync();
        var documentId = Guid.NewGuid();
        await using (var db = await env.Factory.CreateDbContextAsync())
        {
            var d = new Document
            {
                Id = documentId,
                Sha256 = "b" + Guid.NewGuid().ToString("N").PadRight(63, 'b'),
            };
            db.Add(d);
            db.Add(new VideoEntry { DocumentId = d.Id, Title = "course" });
            await db.SaveChangesAsync();
        }
        var library = new LibraryService(env.Factory);
        var v = (await library.ListAsync(default)).Single(x => x.DocumentId == documentId);
        var tasks = Enumerable
            .Range(0, 2)
            .Select(async _ =>
            {
                try
                {
                    await library.IndexAsync(v.Id, v.Revision, default);
                    return true;
                }
                catch (VideoSearchAI.Domain.Models.DomainException)
                {
                    return false;
                }
            })
            .ToArray();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(x => x));
        await using var check = await env.Factory.CreateDbContextAsync();
        Assert.Equal(1, await check.Jobs.CountAsync(x => x.TargetId == v.Id));
    }

    [IntegrationFact]
    public async Task ExpiredLeaseReclaimsAndNewWorkerResumesStoredCheckpoint()
    {
        await env.InitializeAsync();
        var d = new Document
        {
            Sha256 = "c" + Guid.NewGuid().ToString("N").PadRight(63, 'c'),
            DurationMs = 180000,
        };
        var v = new VideoEntry
        {
            DocumentId = d.Id,
            State = "Analyzing",
            Title = "course",
        };
        var job = new IngestionJob
        {
            DocumentId = d.Id,
            TargetId = v.Id,
            Generation = 1,
            Status = "Running",
            LeaseToken = Guid.NewGuid(),
            LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        await using (var db = await env.Factory.CreateDbContextAsync())
        {
            db.Add(d);
            db.Add(v);
            db.Add(job);
            await db.SaveChangesAsync();
        }
        var h = new FakeHttpHandler(
            (request, count) =>
                request.RequestUri!.AbsolutePath.EndsWith("/start")
                    ? FakeHttpHandler.Json(
                        System.Text.Json.JsonSerializer.Serialize(
                            new ExtractionResult(
                                "Succeeded",
                                null,
                                "{}",
                                [new("test", 70000, 95000, "CREATE INDEX", null, [])]
                            )
                        )
                    )
                    : FakeHttpHandler.Json("{\"status\":\"ok\"}")
        );
        var ai = new AiClient(new HttpClient(h) { BaseAddress = new Uri("http://fake/") });
        var o = Options.Create(new PipelineOptions());
        var first = new DurableWorker(
            env.Factory,
            ai,
            env.Store,
            o,
            NullLogger<DurableWorker>.Instance
        );
        Assert.True(await first.TickAsync(default));
        await using (var db = await env.Factory.CreateDbContextAsync())
        {
            var checkpoint = await db.Jobs.SingleAsync(x => x.Id == job.Id);
            Assert.Equal("Index", checkpoint.Step);
            Assert.NotNull(checkpoint.ResultObjectKey);
            Assert.Null(checkpoint.LeaseToken);
        }
        var restarted = new DurableWorker(
            env.Factory,
            ai,
            env.Store,
            o,
            NullLogger<DurableWorker>.Instance
        );
        Assert.True(await restarted.TickAsync(default));
        await using (var db = await env.Factory.CreateDbContextAsync())
        {
            Assert.Equal("Completed", (await db.Jobs.SingleAsync(x => x.Id == job.Id)).Status);
            Assert.Equal("Ready", (await db.Videos.SingleAsync(x => x.Id == v.Id)).State);
            Assert.Equal(70000, (await db.Segments.SingleAsync(x => x.VideoId == v.Id)).StartMs);
            Assert.Equal(1, (await db.Documents.SingleAsync(x => x.Id == d.Id)).ActiveGeneration);
        }
        Assert.Equal(2, h.Calls);
    }

    [IntegrationFact]
    public async Task RealS3StoresBytesAndUploadHashDeduplicates()
    {
        await env.InitializeAsync();
        var seed =
            Environment.GetEnvironmentVariable("VIDEOSEARCH_SEED")
            ?? Path.GetFullPath(
                "../../../../../seeds/postgresql-course.mp4",
                AppContext.BaseDirectory
            );
        var bytes = await File.ReadAllBytesAsync(seed);
        var options = Options.Create(new PipelineOptions());
        var service = new UploadService(env.Factory, env.Store, new MediaProbe(options), options);
        async Task<UploadDto> Upload()
        {
            await using var stream = new MemoryStream(bytes);
            var file = new Microsoft.AspNetCore.Http.FormFile(
                stream,
                0,
                bytes.Length,
                "file",
                "course.mp4"
            );
            return await service.UploadAsync(file, default);
        }
        var a = await Upload();
        var b = await Upload();
        Assert.False(a.Duplicate);
        Assert.True(b.Duplicate);
        Assert.Equal(a.DocumentId, b.DocumentId);
        await using var db = await env.Factory.CreateDbContextAsync();
        var doc = await db.Documents.SingleAsync(x => x.Id == a.DocumentId);
        using var obj = await env.Store.OpenAsync(doc.ObjectKey, default);
        using var copy = new MemoryStream();
        await obj.ResponseStream.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
        Assert.InRange(doc.DurationMs, 179000, 181000);
    }
}
