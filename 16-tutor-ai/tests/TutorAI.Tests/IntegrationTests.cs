using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TutorAI.Database.Jobs;
using TutorAI.Database.Persistence;
using TutorAI.Domain.DTOs;
using TutorAI.Domain.Entities;
using TutorAI.Domain.Models;
using TutorAI.Infrastructure.Abstractions;
using TutorAI.Services.Jobs;
using TutorAI.Services.Tutoring;
using Xunit;

namespace TutorAI.Tests;

[Collection("Postgres")]
public sealed class IntegrationTests(TutorFixture fixture)
{
    private HttpClient Http => fixture.Http;
    private IDbContextFactory<AppDbContext> Factory =>
        fixture.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();

    private async Task RunJob(Guid id)
    {
        var store = new JobStore(Factory);
        var job = await store.ClaimAsync(default);
        Assert.NotNull(job);
        Assert.Equal(id, job.Id);
        var storage = fixture.App.Services.GetRequiredService<IObjectStore>();
        var worker = new DurableWorker(
            Factory,
            store,
            new DemoAiClient(storage),
            storage,
            NullLogger<DurableWorker>.Instance
        );
        await worker.ProcessAsync(job, default);
    }

    private async Task<SessionResult> Create()
    {
        var r = await Http.PostAsJsonAsync("/api/tutor/sessions", new CreateSessionRequest("RAG"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<SessionResult>())!;
    }

    private async Task<SessionResult> Get(Guid id) =>
        (await Http.GetFromJsonAsync<SessionResult>($"/api/tutor/sessions/{id}"))!;

    private async Task<SessionResult> Exercise(SessionResult s, string kind = "single_choice")
    {
        var r = await Http.PostAsJsonAsync(
            $"/api/tutor/sessions/{s.Id}/exercises",
            new CreateExerciseRequest(kind, s.Revision)
        );
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        await RunJob(j.GetProperty("jobId").GetGuid());
        return await Get(s.Id);
    }

    [Fact]
    public async Task FullFlowHidesRubricGradesAndAdaptsIdempotently()
    {
        var s = await Create();
        var explain = await Http.PostAsJsonAsync(
            $"/api/tutor/sessions/{s.Id}/explain",
            new ExplainRequest("Explain RAG", s.Revision)
        );
        await RunJob(
            (await explain.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetGuid()
        );
        s = await Get(s.Id);
        Assert.NotEmpty(s.Explanation!.Citations);
        for (int i = 0; i < 2; i++)
        {
            s = await Exercise(s);
            var publicResponse = await Http.GetStringAsync(
                $"/api/tutor/exercises/{s.Exercise!.Id}"
            );
            Assert.DoesNotContain("rubric", publicResponse, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "expectedAnswer",
                publicResponse,
                StringComparison.OrdinalIgnoreCase
            );
            var body = new AttemptRequest("Retriever", Guid.NewGuid(), s.Revision);
            var first = await Http.PostAsJsonAsync(
                $"/api/tutor/exercises/{s.Exercise.Id}/attempts",
                body
            );
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var a = await first.Content.ReadFromJsonAsync<AttemptResult>();
            Assert.Equal(1, a!.Score);
            var duplicate = await Http.PostAsJsonAsync(
                $"/api/tutor/exercises/{s.Exercise.Id}/attempts",
                body
            );
            Assert.Equal(a.Id, (await duplicate.Content.ReadFromJsonAsync<AttemptResult>())!.Id);
            var changed = await Http.PostAsJsonAsync(
                $"/api/tutor/exercises/{s.Exercise.Id}/attempts",
                body with
                {
                    Answer = "Browser",
                }
            );
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            s = await Get(s.Id);
            Assert.Equal(i == 0 ? 1 : 2, s.Difficulty);
        }
        await using var db = await Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.AdaptationSteps.CountAsync(x => x.SessionId == s.Id));
        Assert.Equal(
            3,
            await db.Messages.CountAsync(x =>
                db.Conversations.Any(c => c.Id == x.ConversationId && c.SessionId == s.Id)
            )
        );
    }

    [Fact]
    public async Task OpenAnswerUsesDurableJudgeJob()
    {
        var s = await Exercise(await Create(), "open_answer");
        var r = await Http.PostAsJsonAsync(
            $"/api/tutor/exercises/{s.Exercise!.Id}/attempts",
            new AttemptRequest("Retriever", Guid.NewGuid(), s.Revision)
        );
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        await using var db = await Factory.CreateDbContextAsync();
        var a = (await r.Content.ReadFromJsonAsync<AttemptResult>())!;
        var j = await db.IngestionJobs.SingleAsync(x => x.TargetId == a.Id);
        await RunJob(j.Id);
        s = await Get(s.Id);
        Assert.Equal("FeedbackReady", s.State);
        Assert.Equal(1, s.Feedback!.Score);
    }

    [Fact]
    public async Task StaleRevisionConflicts()
    {
        var s = await Create();
        var r = await Http.PostAsJsonAsync(
            $"/api/tutor/sessions/{s.Id}/explain",
            new ExplainRequest("Q", 99)
        );
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task InvalidSessionsAreRejected()
    {
        var r = await Http.PostAsJsonAsync(
            "/api/tutor/sessions",
            new CreateSessionRequest("RAG", 4)
        );
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task UploadHashDedupExtractionStorageAndCleanup()
    {
        const string text = "Official document fixture. RAG retrieves context.";
        async Task<UploadResult> Upload()
        {
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "file", "source.txt");
            var r = await Http.PostAsync("/api/documents", multipart);
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<UploadResult>())!;
        }
        var result = await Upload();
        Assert.False(result.Existing);
        var again = await Upload();
        Assert.True(again.Existing);
        Assert.Equal(result.DocumentId, again.DocumentId);
        await RunJob(result.JobId!.Value);
        Assert.Equal(
            text,
            await Http.GetStringAsync($"/api/documents/{result.DocumentId}/content")
        );
        var add = await Http.PostAsJsonAsync(
            "/api/lessons/sources",
            new AddSourceRequest(
                result.DocumentId,
                "https://learn.microsoft.com/en-us/azure/search/",
                "RAG",
                "Fixture"
            )
        );
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var bad = await Http.PostAsJsonAsync(
            "/api/lessons/sources",
            new AddSourceRequest(result.DocumentId, "https://evil.test/", "RAG", "Fixture")
        );
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var deleted = await Http.DeleteAsync($"/api/documents/{result.DocumentId}");
        await RunJob(
            (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobId").GetGuid()
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Http.GetAsync($"/api/documents/{result.DocumentId}/content")).StatusCode
        );
        var storage = fixture.App.Services.GetRequiredService<IObjectStore>();
        await Assert.ThrowsAsync<Amazon.S3.Model.NoSuchKeyException>(() =>
            storage.OpenReadAsync("originals/" + result.DocumentId, default)
        );
    }

    [Fact]
    public async Task AtomicLeaseClaimAndExpiredReclaim()
    {
        await using var db = await Factory.CreateDbContextAsync();
        var j = new IngestionJob { Kind = "Test" };
        db.Add(j);
        await db.SaveChangesAsync();
        var store = new JobStore(Factory);
        var results = await Task.WhenAll(store.ClaimAsync(default), store.ClaimAsync(default));
        Assert.Single(results, x => x is not null);
        var first = results.Single(x => x is not null)!;
        await db
            .IngestionJobs.Where(x => x.Id == j.Id)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(-1))
            );
        var reclaimed = await store.ClaimAsync(default);
        Assert.NotNull(reclaimed);
        Assert.NotEqual(first.LeaseToken, reclaimed.LeaseToken);
        Assert.False(await store.RenewAsync(first, default));
        await db.IngestionJobs.Where(x => x.Id == j.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task LostLeaseCannotPublish()
    {
        var s = await Create();
        var r = await Http.PostAsJsonAsync(
            $"/api/tutor/sessions/{s.Id}/exercises",
            new CreateExerciseRequest("single_choice", s.Revision)
        );
        var store = new JobStore(Factory);
        var j = (await store.ClaimAsync(default))!;
        await using var db = await Factory.CreateDbContextAsync();
        await db
            .IngestionJobs.Where(x => x.Id == j.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaseToken, Guid.NewGuid()));
        var storage = fixture.App.Services.GetRequiredService<IObjectStore>();
        await new DurableWorker(
            Factory,
            store,
            new DemoAiClient(storage),
            storage,
            NullLogger<DurableWorker>.Instance
        ).ProcessAsync(j, default);
        Assert.Equal(0, await db.Exercises.CountAsync(x => x.SessionId == s.Id));
        await db.IngestionJobs.Where(x => x.Id == j.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task DatabaseEnforcesAttemptUniqueness()
    {
        var s = await Exercise(await Create());
        await using var db = await Factory.CreateDbContextAsync();
        var id = Guid.NewGuid();
        db.Add(
            new Attempt
            {
                ExerciseId = s.Exercise!.Id,
                RequestId = id,
                Answer = "x",
            }
        );
        await db.SaveChangesAsync();
        db.Add(
            new Attempt
            {
                ExerciseId = s.Exercise.Id,
                RequestId = id,
                Answer = "y",
            }
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
