using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InspectAI.Database.Jobs;
using InspectAI.Database.Persistence;
using InspectAI.Domain.Entities;
using InspectAI.Infrastructure.Abstractions;
using InspectAI.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InspectAI.Tests.Integration;

public sealed class WorkflowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<JsonElement> Read(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Post(HttpClient c, string path, object body) =>
        await Read(await c.PostAsJsonAsync(path, body));

    private static async Task<JsonElement> Wait(
        HttpClient c,
        string path,
        Func<JsonElement, bool> ready
    )
    {
        for (var n = 0; n < 100; n++)
        {
            var x = await Read(await c.GetAsync(path));
            if (ready(x))
                return x;
            await Task.Delay(150);
        }
        throw new TimeoutException(path);
    }

    private static async Task<Guid> Upload(HttpClient c, string name, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(content), "file", name);
        return (await Read(await c.PostAsync("/api/documents", form)))
            .GetProperty("documentId")
            .GetGuid();
    }

    [IntegrationFact]
    public async Task RealDatabaseAndS3Workflow()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid id,
            photo,
            manual;
        int revision;
        using (var app = new AppFactory(connectionString: database.ConnectionString))
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }
            using var c = app.CreateClient();
            var png = await File.ReadAllBytesAsync(
                Path.Combine(AppContext.BaseDirectory, "seeds", "inspection.png")
            );
            photo = await Upload(c, "inspection.png", png);
            manual = await Upload(
                c,
                "manual.txt",
                Encoding.UTF8.GetBytes(
                    "Inspect the brown surface stain. Before any cleaning, consult a qualified technician and the approved isolation procedure."
                )
            );
            Assert.Equal(photo, await Upload(c, "duplicate.png", png));
            await Wait(
                c,
                $"/api/documents/{manual}",
                d => d.GetProperty("status").GetString() == "Ready"
            );
            await Wait(
                c,
                $"/api/documents/{photo}",
                d => d.GetProperty("status").GetString() == "Ready"
            );
            Assert.Equal(png, await c.GetByteArrayAsync($"/api/documents/{photo}/content"));
            var i = await Post(
                c,
                "/api/inspections",
                new
                {
                    equipmentLabel = "Seed pump",
                    equipmentModel = "P-100",
                    manualVersion = "1.0",
                }
            );
            id = i.GetProperty("id").GetGuid();
            var mismatch = await c.PostAsJsonAsync(
                $"/api/inspections/{id}/sources",
                new
                {
                    documentId = manual,
                    role = "manual",
                    revision = 1,
                    manualModel = "OTHER",
                    manualVersion = "1.0",
                }
            );
            Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
            i = await Post(
                c,
                $"/api/inspections/{id}/sources",
                new
                {
                    documentId = photo,
                    role = "photo",
                    revision = 1,
                }
            );
            i = await Post(
                c,
                $"/api/inspections/{id}/sources",
                new
                {
                    documentId = manual,
                    role = "manual",
                    revision = 2,
                    manualModel = "P-100",
                    manualVersion = "1.0",
                }
            );
            var requestId = Guid.NewGuid();
            await Post(c, $"/api/inspections/{id}/analyze", new { revision = 3, requestId });
            await Post(c, $"/api/inspections/{id}/analyze", new { revision = 3, requestId });
            i = await Wait(
                c,
                $"/api/inspections/{id}",
                d => d.GetProperty("state").GetString() == "NeedsReview"
            );
            Assert.Single(i.GetProperty("findings").EnumerateArray());
            Assert.Single(i.GetProperty("checklist").EnumerateArray());
            Assert.Equal(1, app.Services.GetRequiredService<DeterministicAiClient>().Calls);
            using var report = JsonDocument.Parse(
                i.GetProperty("report").GetProperty("resultJson").GetString()!
            );
            Assert.Contains("pată brună", report.RootElement.GetProperty("summary").GetString());
            var checklist = i.GetProperty("checklist")[0];
            var finding = i.GetProperty("findings")[0];
            Assert.Equal(
                finding.GetProperty("id").GetGuid(),
                checklist.GetProperty("findingId").GetGuid()
            );
            Assert.Equal(photo, finding.GetProperty("documentId").GetGuid());
            revision = i.GetProperty("revision").GetInt32();
            var stale = await c.PatchAsJsonAsync(
                $"/api/inspections/{id}/checklist/{checklist.GetProperty("id").GetGuid()}",
                new { status = "noted", revision = 1 }
            );
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var noted = await Read(
                await c.PatchAsJsonAsync(
                    $"/api/inspections/{id}/checklist/{checklist.GetProperty("id").GetGuid()}",
                    new { status = "noted", revision }
                )
            );
            revision = noted.GetProperty("revision").GetInt32();
            var qid = Guid.NewGuid();
            var answer = await Post(
                c,
                $"/api/inspections/{id}/questions",
                new
                {
                    question = "What does the manual say?",
                    requestId = qid,
                    revision,
                }
            );
            await Post(
                c,
                $"/api/inspections/{id}/questions",
                new
                {
                    question = "What does the manual say?",
                    requestId = qid,
                    revision,
                }
            );
            Assert.Single(answer.GetProperty("evidence").EnumerateArray());
            await Read(await c.GetAsync($"/api/inspections/{id}/export"));
            await using (var db = await factory.CreateDbContextAsync())
            {
                Assert.Single(
                    await db.InspectionReports.Where(x => x.InspectionId == id).ToArrayAsync()
                );
                Assert.Single(
                    await db.QuestionMessages.Where(x => x.InspectionId == id).ToArrayAsync()
                );
            }
        }
        // New application host reuses real PostgreSQL/S3 state after worker/API restart.
        using (var restarted = new AppFactory(connectionString: database.ConnectionString))
        {
            using var c = restarted.CreateClient();
            var i = await Read(await c.GetAsync($"/api/inspections/{id}"));
            Assert.Equal("NeedsReview", i.GetProperty("state").GetString());
            Assert.Single(i.GetProperty("questions").EnumerateArray());
            await Read(await c.DeleteAsync($"/api/documents/{manual}"));
            i = await Read(await c.GetAsync($"/api/inspections/{id}"));
            Assert.Equal("Stale", i.GetProperty("state").GetString());
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await c.GetAsync($"/api/documents/{manual}/content")).StatusCode
            );
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await c.GetAsync($"/api/inspections/{id}/export")).StatusCode
            );
            var f = restarted.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            for (var n = 0; n < 100; n++)
            {
                await using var db = await f.CreateDbContextAsync();
                if (
                    await db.IngestionJobs.AnyAsync(x =>
                        x.DocumentId == manual && x.Kind == "Cleanup" && x.Status == "Succeeded"
                    )
                )
                    break;
                await Task.Delay(100);
            }
            await Assert.ThrowsAnyAsync<Exception>(() =>
                restarted
                    .Services.GetRequiredService<IObjectStore>()
                    .OpenReadAsync("originals/" + manual, default)
            );
        }
    }

    [IntegrationFact]
    public async Task UniqueConstraintConcurrencyAndLease()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var app = new AppFactory(false, database.ConnectionString);
        var f = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await f.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        var doc = new Document { Sha256 = "ABC", FileName = "manual.txt" };
        db.Add(doc);
        db.Add(new IngestionJob { DocumentId = doc.Id });
        await db.SaveChangesAsync();
        var store = app.Services.GetRequiredService<JobStore>();
        var claimed = await store.ClaimAsync(default);
        Assert.NotNull(claimed);
        Assert.Null(await store.ClaimAsync(default));
        await db
            .IngestionJobs.Where(x => x.Id == claimed.Id)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(-1))
            );
        var reclaimed = await store.ClaimAsync(default);
        Assert.NotEqual(claimed.LeaseToken, reclaimed!.LeaseToken);
        Assert.False(await store.RenewAsync(claimed, default));
        db.Add(new Document { Sha256 = "ABC", FileName = "duplicate.txt" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var i = new Inspection
        {
            EquipmentLabel = "pump",
            EquipmentModel = "P-100",
            ManualVersion = "1.0",
        };
        db.Add(i);
        await db.SaveChangesAsync();
        await using var second = await f.CreateDbContextAsync();
        var other = await second.Inspections.SingleAsync(x => x.Id == i.Id);
        i.Revision++;
        await db.SaveChangesAsync();
        other.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
