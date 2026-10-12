using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContractAI.Database.Jobs;
using ContractAI.Database.Persistence;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Entities;
using ContractAI.Infrastructure.Abstractions;
using ContractAI.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContractAI.Tests.Integration;

public sealed class WorkflowTests
{
    [IntegrationFact]
    public async Task RealDatabaseAndS3WorkflowSurvivesRestartAndEnforcesScope()
    {
        using var app = new AppFactory();
        var client = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
        }
        var key = "test-" + Guid.NewGuid();
        var v1 = await UploadVersion(client, key, "V1", 30);
        var v2 = await UploadVersion(client, key, "V2", 15);
        var duplicate = await client.PostAsJsonAsync(
            "/api/contracts/versions",
            new CreateVersionRequest(v2.DocumentId, key, "V2", null)
        );
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var stale = await client.PostAsJsonAsync(
            $"/api/contracts/versions/{v2.Id}/analyze",
            new AnalyzeRequest(99)
        );
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        foreach (var v in new[] { v1, v2 })
        {
            var analyze = await client.PostAsJsonAsync(
                $"/api/contracts/versions/{v.Id}/analyze",
                new AnalyzeRequest(v.Revision)
            );
            analyze.EnsureSuccessStatusCode();
            var job = await analyze.Content.ReadFromJsonAsync<JsonElement>();
            await Wait(
                client,
                "/api/jobs/" + job.GetProperty("jobId").GetGuid(),
                "status",
                "Succeeded"
            );
        }
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/contracts/versions/{v2.Id}");
        var obligation = Assert.Single(detail.GetProperty("obligations").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, obligation.GetProperty("dueDate").ValueKind);
        Assert.Contains("15 zile", obligation.GetProperty("relativeTerm").GetString());
        var compare = await client.PostAsJsonAsync(
            "/api/contracts/compare",
            new CompareRequest(v1.Id, v2.Id)
        );
        compare.EnsureSuccessStatusCode();
        var comparisonId = (await compare.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("comparisonId")
            .GetGuid();
        var compared = await Wait(
            client,
            $"/api/contracts/comparisons/{comparisonId}",
            "state",
            "Ready"
        );
        var pair = Assert.Single(
            compared.GetProperty("result").GetProperty("pairs").EnumerateArray(),
            x => x.GetProperty("kind").GetString() == "modified"
        );
        Assert.Contains("30 zile", pair.GetProperty("beforeText").GetString());
        Assert.Contains("15 zile", pair.GetProperty("afterText").GetString());
        Assert.Equal(
            2,
            compared
                .GetProperty("result")
                .GetProperty("findings")[0]
                .GetProperty("evidence")
                .GetArrayLength()
        );
        var repeat = await client.PostAsJsonAsync(
            "/api/contracts/compare",
            new CompareRequest(v1.Id, v2.Id)
        );
        Assert.Equal(
            comparisonId,
            (await repeat.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("comparisonId")
                .GetGuid()
        );
        var q = new QuestionRequest(Guid.NewGuid(), "Care este termenul de plată?");
        var response = await client.PostAsJsonAsync(
            $"/api/contracts/versions/{v2.Id}/questions",
            q
        );
        response.EnsureSuccessStatusCode();
        var answer = (await response.Content.ReadFromJsonAsync<AnswerDto>())!;
        Assert.All(answer.Citations, c => Assert.Equal(v2.Id, c.VersionId));
        var noConversationReplay = await client.PostAsJsonAsync(
            $"/api/contracts/versions/{v2.Id}/questions",
            q
        );
        Assert.Equal(
            answer.MessageId,
            (await noConversationReplay.Content.ReadFromJsonAsync<AnswerDto>())!.MessageId
        );
        var calls = app.Services.GetRequiredService<DeterministicAiClient>().Calls;
        var dedup = await client.PostAsJsonAsync(
            $"/api/contracts/versions/{v2.Id}/questions",
            q with
            {
                ConversationId = answer.ConversationId,
            }
        );
        Assert.Equal(
            answer.MessageId,
            (await dedup.Content.ReadFromJsonAsync<AnswerDto>())!.MessageId
        );
        Assert.Equal(calls, app.Services.GetRequiredService<DeterministicAiClient>().Calls);
        var foreign = await client.PostAsJsonAsync(
            $"/api/contracts/versions/{v1.Id}/questions",
            new QuestionRequest(Guid.NewGuid(), "Termen?", answer.ConversationId)
        );
        Assert.Equal(HttpStatusCode.Conflict, foreign.StatusCode);
        var unknown = await client.PostAsJsonAsync(
            $"/api/contracts/versions/{v2.Id}/questions",
            new QuestionRequest(Guid.NewGuid(), "Care este jurisdicția?")
        );
        Assert.Empty((await unknown.Content.ReadFromJsonAsync<AnswerDto>())!.Citations);
        var content = await client.GetAsync($"/api/documents/{v2.DocumentId}/content");
        content.EnsureSuccessStatusCode();
        Assert.Contains("15 zile", await content.Content.ReadAsStringAsync());
        // Stop the original API and create a new host over the same real storage.
        await app.DisposeAsync();
        await using var restarted = new AppFactory();
        var again = restarted.CreateClient();
        var restored = await again.GetFromJsonAsync<JsonElement>(
            $"/api/contracts/versions/{v2.Id}"
        );
        Assert.Equal("Ready", restored.GetProperty("version").GetProperty("state").GetString());
        Assert.Single(restored.GetProperty("obligations").EnumerateArray());
        var del = await again.DeleteAsync($"/api/documents/{v2.DocumentId}");
        del.EnsureSuccessStatusCode();
        var cleanup = (await del.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("jobId")
            .GetGuid();
        await Wait(again, "/api/jobs/" + cleanup, "status", "Succeeded");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await again.GetAsync($"/api/documents/{v2.DocumentId}/content")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await again.PostAsJsonAsync(
                    $"/api/contracts/versions/{v2.Id}/questions",
                    new QuestionRequest(Guid.NewGuid(), "Termen?")
                )
            ).StatusCode
        );
        using var reupload = new MultipartFormDataContent();
        reupload.Add(
            new ByteArrayContent(await content.Content.ReadAsByteArrayAsync()),
            "file",
            "same-again.txt"
        );
        var uploadedAgain = await again.PostAsync("/api/documents", reupload);
        uploadedAgain.EnsureSuccessStatusCode();
        Assert.NotEqual(
            v2.DocumentId,
            (await uploadedAgain.Content.ReadFromJsonAsync<UploadResult>())!.DocumentId
        );
        var oldComparison = await again.GetFromJsonAsync<JsonElement>(
            $"/api/contracts/comparisons/{comparisonId}"
        );
        Assert.Equal("Stale", oldComparison.GetProperty("state").GetString());
    }

    [IntegrationFact]
    public async Task AtomicLeaseClaimReclaimsExpiredJobsAndRejectsOldOwner()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var app = new AppFactory(false, database.ConnectionString);
        _ = app.CreateClient();
        var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        // Claim test uses future due time then an expired lease; the production worker cannot win while held.
        var id = Guid.NewGuid();
        var oldToken = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.MigrateAsync();
            db.Add(
                new IngestionJob
                {
                    Id = id,
                    Kind = "LeaseTest",
                    Status = "Running",
                    LeaseToken = oldToken,
                    LeaseUntilUtc = DateTimeOffset.UtcNow.AddSeconds(90),
                }
            );
            await db.SaveChangesAsync();
        }
        var store = new JobStore(factory);
        Assert.False(
            await store.RenewAsync(
                new IngestionJob { Id = id, LeaseToken = Guid.NewGuid() },
                default
            )
        );
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db
                .IngestionJobs.Where(x => x.Id == id)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddSeconds(-10))
                );
        }
        var claimed = await store.ClaimAsync(default);
        Assert.NotNull(claimed);
        Assert.Equal(id, claimed.Id);
        Assert.NotEqual(oldToken, claimed.LeaseToken);
        Assert.False(
            await store.RenewAsync(new IngestionJob { Id = id, LeaseToken = oldToken }, default)
        );
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.IngestionJobs.Where(x => x.Id == id).ExecuteDeleteAsync();
        }
    }

    private static async Task<ContractVersion> UploadVersion(
        HttpClient client,
        string key,
        string label,
        int days
    )
    {
        var text =
            $"1. Părți\nPrestatorul: Demo. Beneficiarul: Test {key}.\n\n2. Plata\nBeneficiarul plătește suma de 1000 EUR în {days} zile de la recepție.";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "file", label + ".txt");
        var upload = await client.PostAsync("/api/documents", multipart);
        upload.EnsureSuccessStatusCode();
        var d = (await upload.Content.ReadFromJsonAsync<UploadResult>())!;
        using var same = new MultipartFormDataContent();
        same.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "file", "same.txt");
        var dupe = await client.PostAsync("/api/documents", same);
        Assert.Equal(HttpStatusCode.OK, dupe.StatusCode);
        Assert.Equal(
            d.DocumentId,
            (await dupe.Content.ReadFromJsonAsync<UploadResult>())!.DocumentId
        );
        await Wait(client, $"/api/documents/{d.DocumentId}", "status", "Ready");
        var created = await client.PostAsJsonAsync(
            "/api/contracts/versions",
            new CreateVersionRequest(d.DocumentId, key, label, null)
        );
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<ContractVersion>())!;
    }

    private static async Task<JsonElement> Wait(
        HttpClient client,
        string path,
        string property,
        string expected
    )
    {
        for (int i = 0; i < 100; i++)
        {
            var result = await client.GetFromJsonAsync<JsonElement>(path);
            var state = result.GetProperty(property).GetString();
            if (state == expected)
                return result;
            Assert.NotEqual("Failed", state);
            await Task.Delay(200);
        }
        throw new TimeoutException(path);
    }
}
