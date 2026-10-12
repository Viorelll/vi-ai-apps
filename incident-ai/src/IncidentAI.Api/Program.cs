using System.Text.Json;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using IncidentAI.Api.Contracts;
using IncidentAI.Api.Services;
using IncidentAI.Application.Contracts;
using IncidentAI.Application.Services;
using IncidentAI.Domain.Entities;
using IncidentAI.Infrastructure.Persistence;
using IncidentAI.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("App") ?? "Host=localhost;Database=incidentai;Username=incidentai;Password=incidentai";
builder.Services.AddDbContext<IncidentDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    new BasicAWSCredentials(builder.Configuration["Storage:AccessKey"] ?? "incidentai-local", builder.Configuration["Storage:SecretKey"] ?? "replace-with-a-long-local-password"),
    new AmazonS3Config { ServiceURL = builder.Configuration["Storage:Endpoint"] ?? "http://localhost:9000", ForcePathStyle = true, AuthenticationRegion = "us-east-1" }));
builder.Services.AddSingleton<IObjectStore, S3ObjectStore>();
builder.Services.AddHttpClient<AiGatewayClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5081/");
    http.Timeout = TimeSpan.FromSeconds(90);
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();

// The one-shot Compose job and local API startup both apply any pending EF migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
    var retries = 0;
    while (true)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch when (++retries < 20) { await Task.Delay(TimeSpan.FromSeconds(2)); }
    }
}
if (args.Contains("--migrate", StringComparer.Ordinal)) return;

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "IncidentAI.Api" }));

app.MapPost("/api/incidents", async (CreateIncidentRequest request, IncidentDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Service) || request.Service.Length > 160 || request.FromUtc >= request.ToUtc
        || request.ToUtc - request.FromUtc > TimeSpan.FromHours(24))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["incident"] = ["Provide a service and a valid UTC interval no longer than 24 hours."] });
    var incident = new Incident { Service = request.Service.Trim(), FromUtc = request.FromUtc.ToUniversalTime(), ToUtc = request.ToUtc.ToUniversalTime() };
    db.Incidents.Add(incident); await db.SaveChangesAsync(ct);
    return Results.Created($"/api/incidents/{incident.Id}", new { incident.Id });
});

app.MapPost("/api/incidents/demo", async (IncidentDbContext db, CancellationToken ct) =>
{
    var start = new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);
    var incident = new Incident { Service = "checkout-api", FromUtc = start, ToUtc = start.AddHours(1) };
    incident.Events.AddRange([
        Event(incident.Id, start, "Information", "T2", "GET /health completed status=200 durationMs=12"),
        Event(incident.Id, start.AddMinutes(2), "Error", "T1", "Payment dependency timeout after 3000ms; requestId=req-001"),
        Event(incident.Id, start.AddMinutes(2).AddSeconds(4), "Error", "T1", "Payment dependency timeout after 3000ms; userEmail=alex@example.com"),
        Event(incident.Id, start.AddMinutes(2).AddSeconds(9), "Error", "T1", "Payment dependency timeout after 3000ms; api_key=sample-secret-value"),
        Event(incident.Id, start.AddMinutes(3), "Information", "T2", "GET /catalog completed status=200 durationMs=18")
    ]);
    incident.Releases.Add(new ReleaseSnapshot { IncidentId = incident.Id, Label = "R2", DeployedAtUtc = start });
    db.Incidents.Add(incident); await db.SaveChangesAsync(ct);
    return Results.Created($"/api/incidents/{incident.Id}", new { incident.Id });
});

app.MapPost("/api/incidents/{id:guid}/logs", async (Guid id, HttpRequest request, IncidentDbContext db, IObjectStore store, CancellationToken ct) =>
{
    var incident = await db.Incidents.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (incident is null) return Results.NotFound();
    if (request.ContentLength is > 5_242_880) return Results.Problem("Log upload exceeds the 5 MiB limit.", statusCode: 413);
    var fileName = Path.GetFileName(request.Query["fileName"].ToString());
    if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || !new[] { ".jsonl", ".ndjson", ".log", ".txt" }.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
        return Results.Problem("Only JSONL, NDJSON, LOG and TXT files are accepted.", statusCode: 415);
    using var body = new MemoryStream();
    var buffer = new byte[65536];
    int bytesRead;
    while ((bytesRead = await request.Body.ReadAsync(buffer, ct)) > 0)
    {
        if (body.Length + bytesRead > 5_242_880) return Results.Problem("Log upload exceeds the 5 MiB limit.", statusCode: 413);
        await body.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
    }
    if (body.GetBuffer().AsSpan(0, checked((int)body.Length)).Contains((byte)0)) return Results.Problem("The uploaded file must be UTF-8 text.", statusCode: 415);
    body.Position = 0;
    var hash = Convert.ToHexString(SHA256.HashData(body.ToArray())).ToLowerInvariant();
    var source = new LogSource { IncidentId = id, FileName = fileName, ContentType = "application/x-ndjson", Sha256 = hash, ObjectKey = $"incidents/{id}/logs/{Guid.NewGuid():N}/{fileName}", SizeBytes = body.Length };
    body.Position = 0;
    await store.PutAsync(source.ObjectKey, body, source.ContentType, ct);
    db.LogSources.Add(source);
    body.Position = 0;
    using var reader = new StreamReader(body, leaveOpen: true);
    var importedEvents = new List<LogEvent>();
    var count = 0;
    while (await reader.ReadLineAsync(ct) is { } line)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        if (++count > 10000) return Results.Problem("Log file exceeds 10,000 events.", statusCode: 413);
        try
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            var at = root.TryGetProperty("timestampUtc", out var ts) || root.TryGetProperty("timestamp", out ts)
                ? DateTimeOffset.Parse(ts.GetString()!, System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime() : incident.FromUtc;
            var severity = root.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "Information" : "Information";
            var trace = root.TryGetProperty("traceId", out var tid) ? tid.GetString() : null;
            var message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? line : line;
            importedEvents.Add(new LogEvent { IncidentId = incident.Id, LogSourceId = source.Id, TimestampUtc = at, Severity = severity[..Math.Min(24, severity.Length)], TraceId = trace?[..Math.Min(128, trace.Length)], MessageRedacted = Redactor.Redact(message)[..Math.Min(8192, Redactor.Redact(message).Length)], SourceId = $"E-{Guid.NewGuid():N}"[..18] });
        }
        catch (JsonException) { importedEvents.Add(new LogEvent { IncidentId = incident.Id, LogSourceId = source.Id, TimestampUtc = incident.FromUtc, Severity = "Information", MessageRedacted = Redactor.Redact(line)[..Math.Min(8192, Redactor.Redact(line).Length)], SourceId = $"E-{Guid.NewGuid():N}"[..18] }); }
        catch (FormatException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["timestamp"] = ["A log timestamp is invalid."] }); }
    }
    db.LogEvents.AddRange(importedEvents);
    await db.SaveChangesAsync(ct);
    return Results.Accepted($"/api/incidents/{id}", new { incidentId = id, documentId = source.Id, imported = count, sha256 = hash });
});

app.MapGet("/api/documents/{id:guid}/content", async (Guid id, IncidentDbContext db, IObjectStore store, CancellationToken ct) =>
{
    var source = await db.LogSources.FindAsync([id], ct);
    if (source is null) return Results.NotFound();
    var item = await store.GetAsync(source.ObjectKey, ct);
    return Results.Stream(item.ResponseStream, source.ContentType, source.FileName, enableRangeProcessing: false);
});

app.MapPost("/api/incidents/{id:guid}/releases", async (Guid id, AddReleaseRequest request, IncidentDbContext db, CancellationToken ct) =>
{
    var incident = await db.Incidents.FindAsync([id], ct);
    if (incident is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Label) || request.Label.Length > 160) return Results.BadRequest(new { error = "Release label is required." });
    var release = new ReleaseSnapshot { IncidentId = id, Label = request.Label.Trim(), DeployedAtUtc = request.DeployedAtUtc?.ToUniversalTime() };
    db.Releases.Add(release); await db.SaveChangesAsync(ct); return Results.Created($"/api/incidents/{id}", release);
});

app.MapPost("/api/incidents/{id:guid}/diagnose", async (Guid id, IncidentDbContext db, AiGatewayClient ai, CancellationToken ct) =>
{
    var incident = await db.Incidents.Include(x => x.Events).Include(x => x.Releases).FirstOrDefaultAsync(x => x.Id == id, ct);
    if (incident is null) return Results.NotFound();
    var events = incident.Events.Where(x => x.TimestampUtc >= incident.FromUtc && x.TimestampUtc < incident.ToUtc).OrderBy(x => x.TimestampUtc).Take(10000).ToArray();
    var modelEvents = events.OrderByDescending(x => x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
            || x.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase))
        .ThenBy(x => x.TimestampUtc).Take(100).OrderBy(x => x.TimestampUtc)
        .Select(x => new LogPoint(x.SourceId, x.TimestampUtc, x.Severity, x.TraceId,
            x.MessageRedacted.Length > 512 ? x.MessageRedacted[..512] : x.MessageRedacted)).ToArray();
    var input = new DiagnosisInput(id, incident.Service, incident.FromUtc, incident.ToUtc,
        modelEvents,
        incident.Releases.Select(x => new ReleasePoint(x.Label, x.DeployedAtUtc)).ToArray());
    var result = await ai.DiagnoseAsync(input, ct);
    var allowed = events.Select(x => x.SourceId).ToHashSet(StringComparer.Ordinal);
    if (result.Hypotheses.Any(h => h.EvidenceIds.Any(e => !allowed.Contains(e)))) return Results.Problem("Diagnosis contained a source outside the incident snapshot.", statusCode: 502);
    var run = new DiagnosisRun { IncidentId = id, State = result.State, Summary = result.Summary, NextChecks = result.NextChecks.ToList() };
    run.Hypotheses = result.Hypotheses.Select((h, i) => new Hypothesis { DiagnosisRunId = run.Id, Rank = i + 1, Claim = h.Claim, ConfidenceLabel = h.ConfidenceLabel, EvidenceIds = h.EvidenceIds.ToList(), Contradictions = h.Contradictions.ToList() }).ToList();
    db.DiagnosisRuns.Add(run); await db.SaveChangesAsync(ct);
    return Results.Ok(new DiagnosisView(run.Id, run.CreatedAtUtc, run.State, run.Summary, result.Hypotheses, run.NextChecks, result.Provider, result.InputTokens, result.OutputTokens, result.DurationMs));
});

app.MapGet("/api/incidents/{id:guid}", async (Guid id, IncidentDbContext db, CancellationToken ct) =>
{
    var incident = await db.Incidents.Include(x => x.Events).Include(x => x.Releases).Include(x => x.LogSources).Include(x => x.Diagnoses).FirstOrDefaultAsync(x => x.Id == id, ct);
    if (incident is null) return Results.NotFound();
    var diagnoses = incident.Diagnoses.OrderByDescending(x => x.CreatedAtUtc).Select(x => new DiagnosisView(x.Id, x.CreatedAtUtc, x.State, x.Summary,
        x.Hypotheses.Select(h => new HypothesisResult(h.Claim, h.ConfidenceLabel, h.EvidenceIds, h.Contradictions)).ToArray(), x.NextChecks, "persisted", 0, 0, 0)).ToArray();
    return Results.Ok(new
    {
        incident = new IncidentView(incident.Id, incident.Service, incident.FromUtc, incident.ToUtc,
        incident.Events.OrderBy(x => x.TimestampUtc).Select(x => new LogEventView(x.SourceId, x.TimestampUtc, x.Severity, x.TraceId, x.MessageRedacted)).ToArray(), diagnoses),
        releases = incident.Releases.Select(x => new ReleasePoint(x.Label, x.DeployedAtUtc)).ToArray(),
        logSources = incident.LogSources.Select(x => new { x.Id, x.FileName, x.Sha256, x.SizeBytes, x.CreatedAtUtc, contentUrl = $"/api/documents/{x.Id}/content" }).ToArray()
    });
});

app.MapGet("/api/incidents/{id:guid}/report", async (Guid id, IncidentDbContext db, CancellationToken ct) =>
{
    var incident = await db.Incidents.Include(x => x.Events).Include(x => x.Diagnoses).FirstOrDefaultAsync(x => x.Id == id, ct);
    if (incident is null) return Results.NotFound();
    return Results.File(JsonSerializer.SerializeToUtf8Bytes(new { incident.Id, incident.Service, incident.FromUtc, incident.ToUtc, events = incident.Events.OrderBy(x => x.TimestampUtc), diagnoses = incident.Diagnoses.OrderByDescending(x => x.CreatedAtUtc) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "application/json", $"incident-{id}.json");
});

app.Run();

static LogEvent Event(Guid incidentId, DateTimeOffset at, string severity, string trace, string message) => new()
{
    IncidentId = incidentId,
    TimestampUtc = at,
    Severity = severity,
    TraceId = trace,
    MessageRedacted = Redactor.Redact(message),
    SourceId = $"E-{Guid.NewGuid():N}"[..18]
};

public partial class Program;
