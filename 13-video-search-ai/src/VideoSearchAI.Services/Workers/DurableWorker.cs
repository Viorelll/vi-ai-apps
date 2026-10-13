using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Entities;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Domain.Services;
using VideoSearchAI.Infrastructure.Ai;
using VideoSearchAI.Infrastructure.Storage;
using VideoSearchAI.Services.Options;
using VideoSearchAI.Services.Services;

namespace VideoSearchAI.Services.Workers;

public sealed class DurableWorker(
    IDbContextFactory<AppDbContext> factory,
    AiClient ai,
    ObjectStore store,
    IOptions<PipelineOptions> options,
    ILogger<DurableWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await TickAsync(stoppingToken))
                    await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError("Worker iteration failed: {ErrorType}", e.GetType().Name);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    public async Task<bool> TickAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var token = Guid.NewGuid();
        var jobs = await db
            .Jobs.FromSqlInterpolated(
                $"""
                UPDATE app."IngestionJobs" SET "Status"='Running', "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '90 seconds'
                WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs"
                 WHERE ("Status" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("Status"='Running' AND "LeaseUntilUtc"<now())
                 ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                """
            )
            .ToListAsync(ct);
        var job = jobs.SingleOrDefault();
        if (job == null)
            return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(65));
        try
        {
            if (job.Kind == "Index")
                await IndexAsync(db, job, deadline.Token);
            else if (job.Kind == "Search")
                await SearchAsync(db, job, deadline.Token);
            else if (job.Kind == "Cleanup")
                await CleanupAsync(db, job, deadline.Token);
            else
                throw new DomainException("invalid_job_kind");
            if (job.LeaseToken != null)
                await SaveOwnedAsync(db, job, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogInformation("Job {JobId} lost lease or source revision", job.Id);
        }
        catch (Exception e)
        {
            var transient =
                e is HttpRequestException or OperationCanceledException
                || e is AiHttpException { Transient: true };
            var code = e switch
            {
                DomainException d => d.Code,
                AiHttpException a => $"ai_http_{a.Status}",
                OperationCanceledException => "ai_timeout",
                _ => "processing_failed",
            };
            logger.LogWarning("Job {JobId}, step {Step}: {Code}", job.Id, job.Step, code);
            db.ChangeTracker.Clear();
            job = await db.Jobs.SingleAsync(x => x.Id == job.Id, ct);
            if (job.LeaseToken != token)
                return true;
            job.Attempt++;
            job.ErrorCode = code;
            // A POST accepted by a provider but with no received operation ID is ambiguous: no blind resubmission.
            var ambiguous =
                job.Kind == "Index"
                && job.Step == "Start"
                && e is HttpRequestException or OperationCanceledException;
            if (transient && !ambiguous && job.Attempt < options.Value.MaxAttempts)
            {
                job.Status = "RetryWaiting";
                var seconds = Math.Min(30, Math.Pow(2, job.Attempt) + Random.Shared.NextDouble());
                if (e is AiHttpException { RetryAfter: not null } a)
                    seconds = Math.Clamp(a.RetryAfter.Value.TotalSeconds, seconds, 300);
                job.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
            }
            else
            {
                job.Status = "Failed";
                if (ambiguous)
                    job.ErrorCode = "extraction_start_uncertain";
                if (job.Kind == "Index")
                {
                    var v = await db.Videos.SingleAsync(x => x.Id == job.TargetId, ct);
                    var d = await db.Documents.SingleAsync(x => x.Id == v.DocumentId, ct);
                    if (!d.Deleted)
                    {
                        v.State = "Failed";
                        v.ErrorCode = job.ErrorCode;
                        v.Revision++;
                        d.Status = "Failed";
                    }
                }
                if (job.Kind == "Search")
                {
                    var r = await db.Runs.SingleAsync(x => x.Id == job.TargetId, ct);
                    r.State = "Failed";
                    r.ErrorCode = job.ErrorCode;
                }
            }
            await SaveOwnedAsync(db, job, ct);
        }
        return true;
    }

    private static async Task SaveOwnedAsync(
        AppDbContext db,
        IngestionJob job,
        CancellationToken ct
    )
    {
        if (
            !await db
                .Jobs.AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == job.Id
                        && x.LeaseToken == job.LeaseToken
                        && x.LeaseUntilUtc > DateTimeOffset.UtcNow,
                    ct
                )
        )
            throw new DbUpdateConcurrencyException();
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        if (job.Status == "Running")
            job.Status = "Queued";
        await db.SaveChangesAsync(ct);
    }

    private async Task IndexAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        var v = await db.Videos.SingleAsync(x => x.Id == job.TargetId, ct);
        var d = await db.Documents.SingleAsync(x => x.Id == v.DocumentId, ct);
        if (d.Deleted)
        {
            job.Status = "Completed";
            return;
        }
        ExtractionResult result;
        if (job.Step == "Start")
        {
            result = await ai.StartAsync(new(job.Id, d.ObjectKey, d.Sha256, d.DurationMs), ct);
            if (result.Status == "Running")
            {
                job.ProviderOperation =
                    result.OperationUrl
                    ?? throw new DomainException("missing_provider_operation", 502);
                job.Step = "Poll";
                job.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(2);
                return;
            }
        }
        else if (job.Step == "Poll")
        {
            result = await ai.PollAsync(
                new(
                    job.ProviderOperation
                        ?? throw new DomainException("missing_provider_operation"),
                    d.DurationMs
                ),
                ct
            );
            if (result.Status == "Running")
            {
                job.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(2);
                return;
            }
        }
        else
        {
            var normalized = await store.ReadTextAsync(
                job.ResultObjectKey ?? throw new DomainException("missing_checkpoint"),
                ct
            );
            result =
                JsonSerializer.Deserialize<ExtractionResult>(normalized)
                ?? throw new DomainException("invalid_checkpoint");
        }
        if (
            result.Status != "Succeeded"
            || result.Sources.Length == 0
            || result.Sources.Length > 2000
        )
            throw new DomainException("invalid_extraction", 502);
        if (job.Step is "Start" or "Poll")
        {
            var prefix = $"artifacts/{d.Id:N}/{job.Generation}/";
            await store.PutTextAsync(prefix + "raw.json", result.RawJson ?? "{}", ct);
            job.ResultObjectKey = prefix + "normalized.json";
            await store.PutTextAsync(job.ResultObjectKey, JsonSerializer.Serialize(result), ct);
            job.Step = "Index";
            v.State = "Indexing";
            v.Revision++;
            return;
        }
        var segments = result
            .Sources.Select(
                (s, i) =>
                {
                    TemporalRules.ToOriginalRange(s.StartMs, s.EndMs, 0, d.DurationMs);
                    if (
                        s.Transcript.Length > 16000
                        || (s.VisualText?.Length ?? 0) > 16000
                        || string.IsNullOrWhiteSpace(s.SourceId)
                    )
                        throw new DomainException("invalid_evidence");
                    return new VideoSegment
                    {
                        Id = TemporalRules.SegmentId(v.Id, job.Generation, i),
                        VideoId = v.Id,
                        Generation = job.Generation,
                        Ordinal = i,
                        StartMs = s.StartMs,
                        EndMs = s.EndMs,
                        Transcript = s.Transcript,
                        VisualText = s.VisualText,
                        SourceId = s.SourceId,
                    };
                }
            )
            .ToArray();
        var dtos = segments.Select(s => MapSegment(s, d.Id)).ToArray();
        await ai.IndexAsync(new(v.Id, d.Id, job.Generation, v.Title, dtos), ct);
        // Recheck deletion/revision after remote work. Revision token also guards the atomic publication.
        if (await db.Documents.AsNoTracking().AnyAsync(x => x.Id == d.Id && x.Deleted, ct))
            throw new DomainException("source_deleted", 409);
        var exists = await db
            .Segments.Where(x => x.VideoId == v.Id && x.Generation == job.Generation)
            .Select(x => x.Id)
            .ToArrayAsync(ct);
        foreach (var s in segments.Where(x => !exists.Contains(x.Id)))
        {
            db.Segments.Add(s);
            foreach (var f in result.Sources[s.Ordinal].Frames)
            {
                if (f.AtMs < s.StartMs || f.AtMs > s.EndMs)
                    throw new DomainException("invalid_frame_timestamp");
                db.Frames.Add(
                    new FrameEvidence
                    {
                        SegmentId = s.Id,
                        AtMs = f.AtMs,
                        Description = f.Description,
                    }
                );
            }
        }
        var oldRuns = await db
            .Runs.Where(r =>
                db.Citations.Any(c =>
                    c.RunId == r.Id && c.DocumentId == d.Id && c.Generation != job.Generation
                )
            )
            .ToListAsync(ct);
        foreach (var r in oldRuns)
            r.State = "Stale";
        d.ActiveGeneration = job.Generation;
        d.Status = "Ready";
        v.State = "Ready";
        v.Revision++;
        v.ErrorCode = null;
        job.Status = "Completed";
        job.Step = "Done";
    }

    private async Task SearchAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        var run = await db.Runs.SingleAsync(x => x.Id == job.TargetId, ct);
        var scope = JsonSerializer.Deserialize<Guid[]>(run.AllowedVideoIdsJson)!;
        var docs = await (
            from v in db.Videos
            join d in db.Documents on v.DocumentId equals d.Id
            where scope.Contains(v.Id) && !d.Deleted && d.Status == "Ready" && v.State == "Ready"
            select new { Video = v, Document = d }
        ).ToArrayAsync(ct);
        if (docs.Length != scope.Length)
            throw new DomainException("sources_not_ready", 409);
        var hits = await ai.RetrieveAsync(
            new(
                run.Question,
                docs.Select(x => x.Document.Id).ToArray(),
                docs.ToDictionary(x => x.Document.Id, x => x.Document.ActiveGeneration)
            ),
            ct
        );
        var allowed = docs.ToDictionary(x => x.Document.Id);
        var hitIds = hits.Where(h =>
                allowed.TryGetValue(h.DocumentId, out var pair)
                && pair.Document.ActiveGeneration == h.Generation
            )
            .OrderByDescending(x => x.Score)
            .Select(x => x.SegmentId)
            .Distinct()
            .Take(8)
            .ToArray();
        var segments = await db
            .Segments.Where(s => hitIds.Contains(s.Id) && scope.Contains(s.VideoId))
            .ToArrayAsync(ct);
        var sources = segments
            .Where(s =>
                docs.Any(p =>
                    p.Video.Id == s.VideoId && p.Document.ActiveGeneration == s.Generation
                )
            )
            .OrderBy(s => Array.IndexOf(hitIds, s.Id))
            .Select(
                (s, i) =>
                    new GroundingSource(
                        $"S{i + 1}",
                        MapSegment(s, docs.Single(x => x.Video.Id == s.VideoId).Document.Id)
                    )
            )
            .ToArray();
        if (
            sources.Sum(s => s.Segment.Transcript.Length + (s.Segment.VisualText?.Length ?? 0))
                + run.Question.Length
            > options.Value.MaxRunTokens * 3
        )
            throw new DomainException("token_budget_exceeded");
        var generated =
            sources.Length == 0
                ? new GeneratedAnswer(
                    "Dovezile disponibile nu acoperă această întrebare.",
                    [],
                    0,
                    0,
                    "none"
                )
                : await ai.GenerateAsync(new(run.Question, sources), ct);
        run.InputTokens += generated.InputTokens;
        run.OutputTokens += generated.OutputTokens;
        if (run.InputTokens + run.OutputTokens > options.Value.MaxRunTokens)
            throw new DomainException("token_budget_exceeded");
        var citations = CitationValidator.Validate(
            generated,
            sources,
            docs.ToDictionary(x => x.Video.Id, x => x.Document.DurationMs)
        );
        // Publication and deletion/reindex use the same source rows; lock only after remote calls.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var p in docs.OrderBy(x => x.Document.Id))
        {
            var current = (
                await db
                    .Documents.FromSqlInterpolated(
                        $"SELECT * FROM app.\"Documents\" WHERE \"Id\"={p.Document.Id} FOR UPDATE"
                    )
                    .AsNoTracking()
                    .ToListAsync(ct)
            ).Single();
            if (
                current.Deleted
                || current.Status != "Ready"
                || current.ActiveGeneration != p.Document.ActiveGeneration
            )
                throw new DomainException("stale_sources", 409);
        }
        db.Citations.RemoveRange(await db.Citations.Where(c => c.RunId == run.Id).ToArrayAsync(ct));
        foreach (var c in citations)
            db.Citations.Add(
                new TemporalCitation
                {
                    RunId = run.Id,
                    SegmentId = c.SegmentId,
                    VideoId = c.VideoId,
                    DocumentId = c.DocumentId,
                    Generation = c.Generation,
                    StartMs = c.StartMs,
                    EndMs = c.EndMs,
                    Label = c.Label,
                    Excerpt = c.Excerpt,
                }
            );
        run.ResultJson = JsonSerializer.Serialize(
            new AnswerDto(
                generated.Answer,
                citations,
                run.InputTokens,
                run.OutputTokens,
                generated.Deployment
            )
        );
        run.State = "Answered";
        job.Status = "Completed";
        job.Step = "Done";
        // Keep result, citations and the owned job completion in one transaction.
        await SaveOwnedAsync(db, job, ct);
        await tx.CommitAsync(ct);
    }

    private async Task CleanupAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        var d = await db.Documents.SingleAsync(x => x.Id == job.TargetId, ct);
        await ai.DeleteAsync(new(d.Id), ct);
        await store.DeleteAsync(d.ObjectKey, ct);
        await store.DeletePrefixAsync($"artifacts/{d.Id:N}/", ct);
        d.Status = "Deleted";
        job.Status = "Completed";
        job.Step = "Done";
    }

    private static SegmentDto MapSegment(VideoSegment s, Guid documentId) =>
        new(
            s.Id,
            s.VideoId,
            documentId,
            s.Generation,
            s.Ordinal,
            s.StartMs,
            s.EndMs,
            s.Transcript,
            s.VisualText,
            s.SourceId
        );
}
