using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TutorAI.Database.Jobs;
using TutorAI.Database.Persistence;
using TutorAI.Domain.DTOs;
using TutorAI.Domain.Entities;
using TutorAI.Domain.Models;
using TutorAI.Infrastructure.Abstractions;
using TutorAI.Services.Tutoring;

namespace TutorAI.Services.Jobs;

public sealed class DurableWorker(
    IDbContextFactory<AppDbContext> factory,
    JobStore jobs,
    IAiClient ai,
    IObjectStore storage,
    ILogger<DurableWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IngestionJob? job = null;
            try
            {
                job = await jobs.ClaimAsync(stoppingToken);
                if (job is not null)
                    await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Worker error {Type}", ex.GetType().Name);
                if (job is not null)
                    await FailAsync(job, ex, stoppingToken);
            }
            await Task.Delay(job is null ? 500 : 50, stoppingToken);
        }
    }

    public async Task ProcessAsync(IngestionJob job, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(70));
        ct = deadline.Token;
        await using var db = await factory.CreateDbContextAsync(ct);
        if (job.Kind is "Extract" or "Cleanup")
        {
            await DocumentAsync(db, job, ct);
            return;
        }
        Attempt? attempt = null;
        Exercise? exercise = null;
        if (job.Kind == "Grade")
        {
            attempt = await db.Attempts.FindAsync([job.TargetId!.Value], ct);
            exercise = await db.Exercises.FindAsync([attempt!.ExerciseId], ct);
        }
        var s = await TutorService.Session(db, exercise?.SessionId ?? job.TargetId!.Value, ct);
        if (s.State == "Stale")
            throw new AppException(409, "stale_sources", "Source was deleted.");
        var sourceQuery = db.LearningSources.Where(x => x.Active && x.Objective == s.Topic);
        if (exercise is not null)
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(
                exercise.SourceIdsJson,
                TutorService.Json
            )!;
            sourceQuery = sourceQuery.Where(x => ids.Contains(x.Id));
        }
        var sources = await sourceQuery
            .Select(x => new SourceEvidence(
                x.Id,
                x.Title,
                x.Objective,
                x.Text,
                x.OfficialUrl,
                x.CheckedAtUtc
            ))
            .Take(8)
            .ToArrayAsync(ct);
        sources = sources
            .Select(x => x with { Text = x.Text.Length > 6000 ? x.Text[..6000] : x.Text })
            .ToArray();
        var request = new AiRequest(
            job.Id,
            job.Kind == "Grade" ? "grade"
                : job.Kind == "Exercise" ? "exercise"
                : "explain",
            s.Topic,
            s.Difficulty,
            job.OperationUrl ?? "",
            sources,
            exercise is null
                ? null
                : JsonSerializer.Deserialize<InternalRubric>(
                    exercise.RubricJson,
                    TutorService.Json
                ),
            attempt?.Answer
        );
        var inputHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, TutorService.Json))
            )
        );
        request = request with { InputHash = inputHash };
        var result = await ai.StepAsync(request, ct);
        var output =
            JsonSerializer.Deserialize<TutorOutput>(result.OutputJson, TutorService.Json)
            ?? throw new AppException(502, "invalid_ai_json", "Empty output.");
        OutputValidator.Validate(output, request);
        // Short transaction after external calls. Lease ownership and session revision guard publication.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await OwnAsync(db, job, ct))
        {
            await tx.RollbackAsync(ct);
            return;
        }
        if (job.Kind == "Exercise")
        {
            var draft = output.Exercise!;
            var e = new Exercise
            {
                SessionId = s.Id,
                Kind = draft.Kind,
                Prompt = draft.Prompt,
                OptionsJson = JsonSerializer.Serialize(draft.Options, TutorService.Json),
                RubricJson = JsonSerializer.Serialize(draft.Rubric, TutorService.Json),
                SourceIdsJson = JsonSerializer.Serialize(
                    sources.Select(x => x.Id),
                    TutorService.Json
                ),
                EvidenceJson = JsonSerializer.Serialize(output.Citations, TutorService.Json),
                Difficulty = s.Difficulty,
            };
            db.Add(e);
            s.CurrentExerciseId = e.Id;
            s.State = "ExerciseReady";
        }
        else if (job.Kind == "Grade")
        {
            attempt!.Score = output.Score;
            attempt.FeedbackJson = result.OutputJson;
            s.State = "FeedbackReady";
            await TutorService.AdaptAsync(db, s, attempt, ct);
        }
        else
        {
            s.ExplanationJson = result.OutputJson;
            s.State =
                s.CurrentExerciseId is null ? "Open"
                : await db.Attempts.AnyAsync(x => x.ExerciseId == s.CurrentExerciseId, ct)
                    ? "FeedbackReady"
                : "ExerciseReady";
        }
        s.ErrorCode = null;
        s.Revision++;
        var conversation = await db.Conversations.SingleAsync(x => x.SessionId == s.Id, ct);
        var message = new Message
        {
            ConversationId = conversation.Id,
            OperationId = job.Id,
            Text = output.Explanation,
            Deployment = result.Deployment,
            InputHash = inputHash,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            DurationMs = result.DurationMs,
        };
        db.Add(message);
        foreach (var c in output.Citations)
        {
            var source = sources.Single(x => x.Id == c.SourceId);
            db.Add(
                new Citation
                {
                    MessageId = message.Id,
                    SourceId = source.Id,
                    OfficialUrl = source.OfficialUrl,
                    Excerpt = c.Quote,
                }
            );
        }
        await db.SaveChangesAsync(ct);
        await CompleteAsync(db, job, ct);
        await tx.CommitAsync(ct);
    }

    private async Task DocumentAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        var d =
            await db.Documents.FindAsync([job.DocumentId!.Value], ct)
            ?? throw new AppException(404, "document_not_found", "Document missing.");
        if (job.Kind == "Cleanup")
        {
            await storage.DeleteAsync(d.ObjectKey, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (!await OwnAsync(db, job, ct))
                return;
            await db.DocumentChunks.Where(x => x.DocumentId == d.Id).ExecuteDeleteAsync(ct);
            // Tombstone retains metadata while excluding future retrieval and content access.
            d.Status = "Deleting";
            await db.SaveChangesAsync(ct);
            await CompleteAsync(db, job, ct);
            await tx.CommitAsync(ct);
            return;
        }
        var extraction = job.OperationUrl is null
            ? await ai.StartAsync(new(d.Id, d.ObjectKey, d.ContentType), ct)
            : await ai.PollAsync(new(job.OperationUrl), ct);
        if (extraction.Status == "Running")
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (!await OwnAsync(db, job, ct))
                return;
            d.Status = "Processing";
            await db.SaveChangesAsync(ct);
            await db
                .IngestionJobs.Where(x => x.Id == job.Id && x.LeaseToken == job.LeaseToken)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, "Queued")
                            .SetProperty(x => x.OperationUrl, extraction.OperationUrl)
                            .SetProperty(
                                x => x.NextAttemptAtUtc,
                                DateTimeOffset.UtcNow.AddSeconds(2)
                            )
                            .SetProperty(x => x.LeaseToken, (Guid?)null),
                    ct
                );
            await tx.CommitAsync(ct);
            return;
        }
        if (extraction.Status != "Succeeded" || extraction.Pages is null)
            throw new AppException(
                422,
                extraction.ErrorCode ?? "extraction_failed",
                "Extraction failed."
            );
        var generation = d.ActiveGeneration + 1;
        var chunks = new List<DocumentChunk>();
        int ordinal = 0;
        foreach (var page in extraction.Pages)
            for (int offset = 0; offset < page.Text.Length; offset += 1500)
            {
                chunks.Add(
                    new()
                    {
                        DocumentId = d.Id,
                        Generation = generation,
                        Ordinal = ordinal++,
                        Text = page.Text.Substring(
                            offset,
                            Math.Min(1500, page.Text.Length - offset)
                        ),
                        PageNumber = page.PageNumber,
                    }
                );
                if (ordinal > 2000)
                    throw new AppException(413, "chunk_limit", "Document has too many chunks.");
            }
        if (chunks.Count == 0)
            throw new AppException(422, "empty_extraction", "No readable content.");
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            if (!await OwnAsync(db, job, ct))
                return;
            db.AddRange(chunks);
            d.Status = "Ready";
            d.ActiveGeneration = generation;
            d.ErrorCode = null;
            await db.SaveChangesAsync(ct);
            await CompleteAsync(db, job, ct);
            await tx.CommitAsync(ct);
        }
    }

    private static async Task<bool> OwnAsync(
        AppDbContext db,
        IngestionJob job,
        CancellationToken ct
    )
    {
        // Lock lease row until result commit so a replacement worker cannot race publication.
        var rows = await db
            .IngestionJobs.FromSqlInterpolated(
                $"SELECT * FROM app.\"IngestionJobs\" WHERE \"Id\"={job.Id} AND \"LeaseToken\"={job.LeaseToken} AND \"Status\"='Running' AND \"LeaseUntilUtc\">now() FOR UPDATE"
            )
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Count == 1;
    }

    private static Task CompleteAsync(AppDbContext db, IngestionJob j, CancellationToken ct) =>
        db
            .IngestionJobs.Where(x => x.Id == j.Id && x.LeaseToken == j.LeaseToken)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, "Completed")
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null),
                ct
            );

    private async Task FailAsync(IngestionJob job, Exception ex, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await OwnAsync(db, job, ct))
            return;
        var code = (ex as AppException)?.Code ?? "processing_failed";
        var retry =
            job.Attempt < 2
            && (
                ex is HttpRequestException or OperationCanceledException
                || ex is AppException { Status: 503 }
            );
        var delay = Math.Max((ex as AppException)?.RetryAfterSeconds ?? 0, 2 * (1 << job.Attempt));
        await db
            .IngestionJobs.Where(x => x.Id == job.Id && x.LeaseToken == job.LeaseToken)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, retry ? "RetryWaiting" : "Failed")
                        .SetProperty(x => x.Attempt, x => x.Attempt + 1)
                        .SetProperty(x => x.ErrorCode, code)
                        .SetProperty(
                            x => x.NextAttemptAtUtc,
                            DateTimeOffset.UtcNow.AddSeconds(delay)
                        )
                        .SetProperty(x => x.LeaseToken, (Guid?)null),
                ct
            );
        if (!retry)
        {
            if (job.DocumentId is { } did)
                await db
                    .Documents.Where(x => x.Id == did && x.Status != "Deleting")
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Status, "Failed")
                                .SetProperty(x => x.ErrorCode, code),
                        ct
                    );
            else
            {
                var sid =
                    job.Kind == "Grade"
                        ? await db
                            .Attempts.Where(x => x.Id == job.TargetId)
                            .Join(db.Exercises, a => a.ExerciseId, e => e.Id, (a, e) => e.SessionId)
                            .SingleAsync(ct)
                        : job.TargetId!.Value;
                await db
                    .TutorSessions.Where(x => x.Id == sid && x.State != "Stale")
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.State, "Failed")
                                .SetProperty(x => x.ErrorCode, code)
                                .SetProperty(x => x.Revision, x => x.Revision + 1),
                        ct
                    );
            }
        }
        await tx.CommitAsync(ct);
    }
}
