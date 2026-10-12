using System.Security.Cryptography;
using MeetingAI.Ai.Contracts;
using MeetingAI.Api.Services;
using MeetingAI.Database;
using MeetingAI.Domain.DTOs;
using MeetingAI.Domain.Entities;
using MeetingAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Amazon.S3;
using Amazon.S3.Model;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 51L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 50L * 1024 * 1024);
builder.Services.AddDbContext<MeetingDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("App") ?? "Host=db;Database=meetingai;Username=meetingai;Password=meetingai-local", p => p.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
builder.Services.AddHttpClient("ai", c => { c.BaseAddress = new Uri(builder.Configuration["AI_BASE_URL"] ?? "http://ai:8080/"); c.Timeout = TimeSpan.FromSeconds(150); });
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new AmazonS3Config { ServiceURL = builder.Configuration["S3_ENDPOINT"] ?? "http://minio:9000", ForcePathStyle = true, AuthenticationRegion = "us-east-1" }));
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (MeetingDbContext db, CancellationToken ct) => { try { await db.Database.CanConnectAsync(ct); return Results.Ok(new { status = "ready" }); } catch { return Results.StatusCode(503); } });
app.MapPost("/api/meetings/demo", async (MeetingDbContext db, CancellationToken ct) =>
{
    var meeting = new Meeting { Title = "Ședință demonstrativă", State = "Ready" };
    meeting.Segments = [new() { MeetingId = meeting.Id, Ordinal = 0, StartMs = 0, EndMs = 2400, SpeakerId = "speaker-1", SpeakerLabel = "Vorbitor 1", Text = "Folosim PostgreSQL.", RawText = "Folosim PostgreSQL." }, new() { MeetingId = meeting.Id, Ordinal = 1, StartMs = 2600, EndMs = 5100, SpeakerId = "speaker-2", SpeakerLabel = "Vorbitor 2", Text = "Pregătesc schema.", RawText = "Pregătesc schema." }];
    meeting.Summary = "Echipa a decis să folosească PostgreSQL și să pregătească schema bazei de date."; meeting.Decisions = [new() { MeetingId = meeting.Id, Text = "Se va folosi PostgreSQL.", SegmentIds = [meeting.Segments[0].Id] }]; meeting.ActionItems = [new() { MeetingId = meeting.Id, Text = "Pregătirea schemei bazei de date.", SegmentIds = [meeting.Segments[1].Id] }]; db.Meetings.Add(meeting); await db.SaveChangesAsync(ct); return Results.Created($"/api/meetings/{meeting.Id}", MeetingMapper.ToDto(meeting));
});
app.MapPost("/api/meetings", async (HttpRequest request, MeetingDbContext db, IAmazonS3 s3, IConfiguration config, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "multipart_form_required" });
    var form = await request.ReadFormAsync(ct); var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "file_required" });
    if (file.Length > 50L * 1024 * 1024) return Results.BadRequest(new { error = "file_too_large" });
    await using var stream = file.OpenReadStream(); var header = new byte[12]; var headerCount = await stream.ReadAsync(header, ct); var contentType = AudioFileValidator.DetectContentType(file.FileName, header.AsSpan(0, headerCount)); if (contentType is null) return Results.BadRequest(new { error = "invalid_audio_content" }); stream.Position = 0; var sha = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant(); stream.Position = 0;
    var duplicate = await db.Meetings.SingleOrDefaultAsync(x => x.Sha256 == sha, ct); if (duplicate is not null) return Results.Ok(new { meetingId = duplicate.Id, sha256 = sha, state = duplicate.State, duplicate = true });
    var id = Guid.NewGuid(); var bucket = config["S3_BUCKET"] ?? "meetingai"; try { await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = $"audio/{id}/{Path.GetFileName(file.FileName)}", InputStream = stream, ContentType = contentType }, ct); } catch (Exception) { return Results.Problem("Object storage is not ready", statusCode: 503); }
    var meeting = new Meeting { Id = id, Title = Path.GetFileNameWithoutExtension(file.FileName), State = "Uploaded", Sha256 = sha, AudioObjectKey = $"audio/{id}/{Path.GetFileName(file.FileName)}", AudioContentType = contentType }; db.Meetings.Add(meeting); await db.SaveChangesAsync(ct); return Results.Accepted($"/api/meetings/{id}", new { meetingId = id, sha256 = sha, state = meeting.State });
});
app.MapGet("/api/meetings/{id:guid}/audio", async (Guid id, HttpContext context, MeetingDbContext db, IAmazonS3 s3, IConfiguration config, CancellationToken ct) =>
{
    var meeting = await db.Meetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (meeting is null || string.IsNullOrWhiteSpace(meeting.AudioObjectKey)) return Results.NotFound();
    var start = 0L; var end = meeting.AudioSizeBytes - 1; var partial = false; context.Response.Headers.AcceptRanges = "bytes";
    if (context.Request.Headers.TryGetValue("Range", out var rangeHeader))
    {
        if (!Microsoft.Net.Http.Headers.RangeHeaderValue.TryParse(rangeHeader.ToString(), out var range) || range.Unit != "bytes" || range.Ranges.Count != 1) { context.Response.Headers.ContentRange = $"bytes */{meeting.AudioSizeBytes}"; return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable); }
        var requested = range.Ranges.First(); if (requested.From is null) { var suffix = requested.To ?? 0; start = Math.Max(0, meeting.AudioSizeBytes - suffix); } else start = requested.From.Value;
        if (start >= meeting.AudioSizeBytes) { context.Response.Headers.ContentRange = $"bytes */{meeting.AudioSizeBytes}"; return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable); }
        end = Math.Min(requested.To ?? (meeting.AudioSizeBytes - 1), meeting.AudioSizeBytes - 1); if (end < start) { context.Response.Headers.ContentRange = $"bytes */{meeting.AudioSizeBytes}"; return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable); }
        partial = true;
    }
    var request = new GetObjectRequest { BucketName = config["S3_BUCKET"] ?? "meetingai", Key = meeting.AudioObjectKey }; if (partial) request.ByteRange = new ByteRange(start, end);
    var original = await s3.GetObjectAsync(request, ct); context.Response.RegisterForDispose(original); if (partial) { context.Response.StatusCode = StatusCodes.Status206PartialContent; context.Response.Headers.ContentRange = $"bytes {start}-{end}/{meeting.AudioSizeBytes}"; }
    return Results.Stream(original.ResponseStream, meeting.AudioContentType ?? "application/octet-stream", enableRangeProcessing: false);
});
app.MapPost("/api/meetings/{id:guid}/analyze", async (Guid id, MeetingDbContext db, IAmazonS3 s3, IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
{
    var meeting = await db.Meetings.Include(x => x.Segments).Include(x => x.Decisions).Include(x => x.ActionItems).SingleOrDefaultAsync(x => x.Id == id, ct); if (meeting is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(meeting.AudioObjectKey)) return Results.BadRequest(new { error = "audio_missing" });
    using var original = await s3.GetObjectAsync(config["S3_BUCKET"] ?? "meetingai", meeting.AudioObjectKey, ct);
    var client = clients.CreateClient("ai"); using var form = new MultipartFormDataContent(); var audio = new StreamContent(original.ResponseStream); audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(meeting.AudioContentType ?? "application/octet-stream"); form.Add(audio, "audio", Path.GetFileName(meeting.AudioObjectKey)); form.Add(new StringContent(id.ToString()), "meetingId"); form.Add(new StringContent(meeting.Title), "title");
    using var response = await client.PostAsync("internal/meetings/analyze-audio", form, ct); if (!response.IsSuccessStatusCode) return Results.Problem("Audio transcription failed. Configure the Microsoft Speech adapter.", statusCode: (int)response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<AnalyzeResponse>(ct); if (result is null) return Results.Problem("Invalid AI response", statusCode: 502);
    var idMap = result.Segments.ToDictionary(s => s.Id, s => Guid.TryParse(s.Id, out var parsed) ? parsed : Guid.NewGuid()); meeting.Segments = result.Segments.Select((s, i) => new TranscriptSegment { Id = idMap[s.Id], MeetingId = id, Ordinal = i, StartMs = s.StartMs, EndMs = s.EndMs, SpeakerId = s.SpeakerId, Text = s.Text, RawText = s.Text }).ToList(); SegmentValidator.Validate(meeting.Segments.Select(s => new SegmentDto(s.Id, s.Ordinal, s.StartMs, s.EndMs, s.SpeakerId, s.SpeakerLabel, s.Text)), long.MaxValue);
    meeting.Summary = result.Summary; meeting.Decisions = result.Decisions.Select(d => new Decision { MeetingId = id, Text = d.Text, SegmentIds = d.SegmentIds.Select(x => idMap.GetValueOrDefault(x, Guid.Empty)).ToList() }).ToList();
    meeting.ActionItems = result.ActionItems.Select(a => new ActionItem { MeetingId = id, Text = a.Text, AssigneeLabel = a.AssigneeLabel, DueText = a.DueText, SegmentIds = a.SegmentIds.Select(x => idMap.GetValueOrDefault(x, Guid.Empty)).ToList() }).ToList(); meeting.State = "Ready"; meeting.TranscriptRevision++; meeting.Revision++; await db.SaveChangesAsync(ct); return Results.Ok(MeetingMapper.ToDto(meeting));
});
app.MapGet("/api/meetings/{id:guid}", async (Guid id, MeetingDbContext db, CancellationToken ct) => { var m = await db.Meetings.Include(x => x.Segments).Include(x => x.Decisions).Include(x => x.ActionItems).SingleOrDefaultAsync(x => x.Id == id, ct); return m is null ? Results.NotFound() : Results.Ok(MeetingMapper.ToDto(m)); });
app.MapPatch("/api/meetings/{id:guid}/transcript", async (Guid id, CorrectionRequest body, MeetingDbContext db, CancellationToken ct) => { var m = await db.Meetings.Include(x => x.Segments).Include(x => x.Decisions).Include(x => x.ActionItems).SingleOrDefaultAsync(x => x.Id == id, ct); if (m is null) return Results.NotFound(); if (body.Revision != m.Revision) return Results.Conflict(new { error = "revision_conflict", revision = m.Revision }); foreach (var c in body.Segments) { var s = m.Segments.FirstOrDefault(x => x.Id == c.Id); if (s is not null) { s.Text = c.Text.Trim(); s.SpeakerLabel = string.IsNullOrWhiteSpace(c.SpeakerLabel) ? null : c.SpeakerLabel.Trim(); } } m.Revision++; m.TranscriptRevision++; m.Decisions.Clear(); m.ActionItems.Clear(); await db.SaveChangesAsync(ct); return Results.Ok(MeetingMapper.ToDto(m)); });
app.MapGet("/api/meetings/{id:guid}/export", async (Guid id, MeetingDbContext db, CancellationToken ct) => { var m = await db.Meetings.Include(x => x.Segments).Include(x => x.Decisions).Include(x => x.ActionItems).SingleOrDefaultAsync(x => x.Id == id, ct); return m is null ? Results.NotFound() : Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(MeetingMapper.ToDto(m), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }), "application/json", $"meeting-{id}.json"); });
if (args.Contains("--migrate", StringComparer.Ordinal)) { await SchemaInitializer.InitializeAsync(app.Services); return; }
if (builder.Configuration.GetValue("Storage:Enabled", true))
    await ObjectStorageInitializer.EnsureBucketAsync(app.Services.GetRequiredService<IAmazonS3>(), builder.Configuration["S3_BUCKET"] ?? "meetingai", CancellationToken.None);
app.Run();
