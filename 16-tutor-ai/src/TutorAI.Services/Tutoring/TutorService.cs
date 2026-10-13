using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TutorAI.Database.Persistence;
using TutorAI.Domain.DTOs;
using TutorAI.Domain.Entities;
using TutorAI.Domain.Models;
using TutorAI.Domain.Rules;

namespace TutorAI.Services.Tutoring;

public sealed class TutorService(IDbContextFactory<AppDbContext> factory, IConfiguration config)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SessionResult> CreateAsync(CreateSessionRequest r, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(r.Topic)
            || r.Topic.Length > 200
            || r.Difficulty is < 1 or > 3
        )
            throw new AppException(400, "invalid_session", "Topic and difficulty 1–3 required.");
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.LearningSources.AnyAsync(x => x.Active && x.Objective == r.Topic, ct))
            throw new AppException(400, "unknown_topic", "Select a documented topic.");
        var s = new TutorSession { Topic = r.Topic, Difficulty = r.Difficulty };
        db.Add(s);
        db.Add(new Conversation { SessionId = s.Id });
        await db.SaveChangesAsync(ct);
        return await GetAsync(s.Id, ct);
    }

    public async Task<SessionResult> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var s = await Session(db, id, ct);
        var e = s.CurrentExerciseId is { } eid ? await db.Exercises.FindAsync([eid], ct) : null;
        var a = e is null
            ? null
            : await db.Attempts.SingleOrDefaultAsync(x => x.ExerciseId == e.Id, ct);
        return new(
            s.Id,
            s.Topic,
            s.Difficulty,
            s.State,
            s.Revision,
            s.ExplanationJson is null
                ? null
                : JsonSerializer.Deserialize<TutorOutput>(s.ExplanationJson, Json),
            e is null ? null : Public(e),
            a is null ? null : Result(a),
            s.ErrorCode,
            config.GetValue<bool>("Ai:DemoMode")
        );
    }

    public async Task<PublicExercise> ExerciseAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return Public(
            await db.Exercises.FindAsync([id], ct)
                ?? throw new AppException(404, "exercise_not_found", "Exercise not found.")
        );
    }

    public async Task<Guid> QueueAsync(
        Guid id,
        string step,
        string input,
        long revision,
        CancellationToken ct
    )
    {
        if (
            step == "explain" && (string.IsNullOrWhiteSpace(input) || input.Length > 2000)
            || step == "exercise" && input is not ("single_choice" or "open_answer")
        )
            throw new AppException(400, "invalid_input", "Invalid question or exercise kind.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var s = await Session(db, id, ct);
        Check(s, revision);
        if (s.State is "Explaining" or "Assessing")
            throw new AppException(409, "session_busy", "Wait for the current operation.");
        if (step == "exercise" && s.State is not ("FeedbackReady" or "Open" or "Failed"))
            throw new AppException(409, "exercise_pending", "Complete the current exercise first.");
        var job = new IngestionJob
        {
            Kind = step == "explain" ? "Explain" : "Exercise",
            TargetId = id,
            OperationUrl = input,
        };
        s.State = "Explaining";
        s.ErrorCode = null;
        s.Revision++;
        db.Add(job);
        await Save(db, ct);
        return job.Id;
    }

    public async Task<AttemptResult> AttemptAsync(Guid eid, AttemptRequest r, CancellationToken ct)
    {
        if (
            r.RequestId == Guid.Empty
            || string.IsNullOrWhiteSpace(r.Answer)
            || r.Answer.Length > 8000
        )
            throw new AppException(
                400,
                "invalid_answer",
                "A bounded answer and requestId are required."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var e =
            await db.Exercises.FindAsync([eid], ct)
            ?? throw new AppException(404, "exercise_not_found", "Exercise not found.");
        // Serialize submissions per session before checking request id and revision.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM app.\"TutorSessions\" WHERE \"Id\"={e.SessionId} FOR UPDATE",
            ct
        );
        var existing = await db.Attempts.SingleOrDefaultAsync(x => x.ExerciseId == eid, ct);
        if (existing is not null)
        {
            if (existing.RequestId != r.RequestId || existing.Answer != r.Answer)
                throw new AppException(
                    409,
                    "already_submitted",
                    "This exercise already has an answer."
                );
            return Result(existing);
        }
        var s = await Session(db, e.SessionId, ct);
        Check(s, r.Revision);
        if (s.State != "ExerciseReady" || s.CurrentExerciseId != eid)
            throw new AppException(409, "exercise_state", "Exercise is not ready.");
        var a = new Attempt
        {
            ExerciseId = eid,
            RequestId = r.RequestId,
            Answer = r.Answer,
        };
        db.Add(a);
        s.Revision++;
        if (e.Kind == "single_choice")
        {
            var rubric = JsonSerializer.Deserialize<InternalRubric>(e.RubricJson, Json)!;
            var score = AnswerGrader.Grade(r.Answer, rubric.ExpectedAnswer);
            var feedback = new TutorOutput(
                score == 1
                    ? "Corect. Răspunsul respectă conceptul din sursa oficială."
                    : "Recitește explicația și sursa citată; răspunsul nu corespunde conceptului.",
                null,
                score,
                [
                    new(
                        "Exact answer",
                        score,
                        score == 1
                            ? "Răspuns corect."
                            : "Răspuns așteptat: " + rubric.ExpectedAnswer
                    ),
                ],
                JsonSerializer.Deserialize<EvidenceCitation[]>(e.EvidenceJson, Json)!
            );
            a.Score = score;
            a.FeedbackJson = JsonSerializer.Serialize(feedback, Json);
            s.State = "FeedbackReady";
            await AdaptAsync(db, s, a, ct);
        }
        else
        {
            s.State = "Assessing";
            db.Add(new IngestionJob { Kind = "Grade", TargetId = a.Id });
        }
        await Save(db, ct);
        await tx.CommitAsync(ct);
        return Result(a);
    }

    public async Task<Guid> RetryAsync(Guid id, long revision, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var s = await Session(db, id, ct);
        Check(s, revision);
        if (s.State != "Failed")
            throw new AppException(409, "session_state", "Only a failed operation can be retried.");
        var attempts = await db
            .Attempts.Where(a => db.Exercises.Any(e => e.Id == a.ExerciseId && e.SessionId == id))
            .Select(a => a.Id)
            .ToArrayAsync(ct);
        var j =
            await db
                .IngestionJobs.Where(j =>
                    j.Status == "Failed"
                    && (j.TargetId == id || attempts.Contains(j.TargetId ?? Guid.Empty))
                )
                .OrderByDescending(j => j.NextAttemptAtUtc)
                .FirstOrDefaultAsync(ct)
            ?? throw new AppException(409, "job_missing", "No failed job found.");
        j.Status = "Queued";
        j.Attempt = 0;
        j.ErrorCode = null;
        j.NextAttemptAtUtc = DateTimeOffset.UtcNow;
        s.State = j.Kind == "Grade" ? "Assessing" : "Explaining";
        s.ErrorCode = null;
        s.Revision++;
        await Save(db, ct);
        return j.Id;
    }

    internal static async Task AdaptAsync(
        AppDbContext db,
        TutorSession s,
        Attempt a,
        CancellationToken ct
    )
    {
        var scores = await db
            .Attempts.Where(x =>
                x.Score != null
                && db.Exercises.Any(e => e.Id == x.ExerciseId && e.SessionId == s.Id)
            )
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.Score!.Value)
            .Take(1)
            .ToListAsync(ct);
        scores.Reverse();
        scores.Add(a.Score!.Value);
        var next = DifficultyRule.Next(s.Difficulty, scores);
        db.Add(
            new AdaptationStep
            {
                SessionId = s.Id,
                AttemptId = a.Id,
                PreviousLevel = s.Difficulty,
                NextLevel = next,
            }
        );
        s.Difficulty = next;
    }

    internal static async Task<TutorSession> Session(
        AppDbContext db,
        Guid id,
        CancellationToken ct
    ) =>
        await db.TutorSessions.FindAsync([id], ct)
        ?? throw new AppException(404, "session_not_found", "Session not found.");

    internal static PublicExercise Public(Exercise e) =>
        new(
            e.Id,
            e.Kind,
            e.Prompt,
            JsonSerializer.Deserialize<string[]>(e.OptionsJson, Json)!,
            e.Difficulty
        );

    internal static AttemptResult Result(Attempt a) =>
        new(
            a.Id,
            a.RequestId,
            a.Score,
            a.FeedbackJson is null
                ? null
                : JsonSerializer.Deserialize<TutorOutput>(a.FeedbackJson, Json)
        );

    internal static void Check(TutorSession s, long revision)
    {
        if (s.Revision != revision)
            throw new AppException(
                409,
                "revision_conflict",
                "Reload the session before continuing."
            );
    }

    internal static async Task Save(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(409, "revision_conflict", "The session changed; reload it.");
        }
    }
}
