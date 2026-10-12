using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VoiceAI.Database.Persistence;
using VoiceAI.Domain.DTOs;
using VoiceAI.Domain.Entities;
using VoiceAI.Domain.Services;
using VoiceAI.Infrastructure.Storage;
using VoiceAI.Services;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 11 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = 11 * 1024 * 1024
);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("App")
            ?? throw new InvalidOperationException("ConnectionStrings:App is required")
    )
);
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    builder.Configuration["Storage:AccessKey"] ?? "voiceai",
    builder.Configuration["Storage:SecretKey"] ?? "replace-minio-password",
    new AmazonS3Config
    {
        ServiceURL = builder.Configuration["Storage:Endpoint"] ?? "http://localhost:9000",
        ForcePathStyle = true,
    }
));
builder.Services.AddSingleton<IObjectStore, S3ObjectStore>();
builder.Services.AddHttpClient<VoiceSessionService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5101/");
    client.Timeout = TimeSpan.FromSeconds(65);
    client.DefaultRequestHeaders.Add(
        "X-Internal-Key",
        builder.Configuration["Internal:Key"] ?? "replace-internal-key"
    );
});
builder.Services.AddHttpClient(
    "ai",
    client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5101/"
        );
        client.Timeout = TimeSpan.FromSeconds(65);
        client.DefaultRequestHeaders.Add(
            "X-Internal-Key",
            builder.Configuration["Internal:Key"] ?? "replace-internal-key"
        );
    }
);
var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope
        .ServiceProvider.GetRequiredService<IObjectStore>()
        .EnsureBucketAsync(CancellationToken.None);
    await SeedSlotsAsync(db);
    return;
}
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    async (AppDbContext db, IObjectStore store, CancellationToken ct) =>
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return Results.StatusCode(503);
            await store.EnsureBucketAsync(ct);
            return Results.Ok(new { status = "ok" });
        }
        catch
        {
            return Results.StatusCode(503);
        }
    }
);
app.MapPost(
    "/api/voice/sessions",
    async (CreateSessionRequest request, VoiceSessionService service, CancellationToken ct) =>
    {
        try
        {
            var session = await service.CreateAsync(request.TimeZoneId, ct);
            return Results.Created(
                $"/api/voice/sessions/{session.Id}",
                new
                {
                    session.Id,
                    session.TimeZoneId,
                    session.State,
                    session.Revision,
                }
            );
        }
        catch (InvalidOperationException e)
        {
            return Results.BadRequest(new { code = e.Message });
        }
    }
);
app.MapGet(
    "/api/voice/slots",
    async (AppDbContext db, CancellationToken ct) =>
        Results.Ok(
            await db
                .AvailabilitySlots.Where(x =>
                    x.StartUtc > DateTimeOffset.UtcNow && x.Booking == null
                )
                .OrderBy(x => x.StartUtc)
                .Select(x => new
                {
                    x.Id,
                    x.StartUtc,
                    x.EndUtc,
                    x.ResourceLabel,
                    x.Revision,
                })
                .Take(30)
                .ToListAsync(ct)
        )
);
app.MapGet(
    "/api/voice/sessions/{id:guid}",
    async (Guid id, AppDbContext db, CancellationToken ct) =>
    {
        var session = await db
            .VoiceSessions.Include(x => x.Turns)
            .Include(x => x.Proposals)
                .ThenInclude(x => x.Slot)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return session is null
            ? Results.NotFound(new { code = "session_not_found" })
            : Results.Ok(
                new
                {
                    session.Id,
                    session.TimeZoneId,
                    session.State,
                    session.Revision,
                    turns = session
                        .Turns.OrderBy(x => x.CreatedAtUtc)
                        .Select(x => new
                        {
                            x.Id,
                            x.Transcript,
                            x.Reply,
                            x.CreatedAtUtc,
                            audioUrl = x.OutputAudioKey == null
                                ? null
                                : $"/api/voice/sessions/{session.Id}/turns/{x.Id}/audio",
                        }),
                    proposals = session
                        .Proposals.OrderByDescending(x => x.Id)
                        .Select(x => new
                        {
                            x.Id,
                            x.State,
                            x.ExpiresAtUtc,
                            x.Revision,
                            x.SlotId,
                            startUtc = x.Slot!.StartUtc,
                            endUtc = x.Slot.EndUtc,
                            x.Slot.ResourceLabel,
                            x.SlotRevision,
                        }),
                }
            );
    }
);
app.MapGet(
    "/api/voice/sessions/{id:guid}/turns/{turnId:guid}/audio",
    async (Guid id, Guid turnId, AppDbContext db, IObjectStore store, CancellationToken ct) =>
    {
        var key = await db
            .VoiceTurns.Where(x => x.SessionId == id && x.Id == turnId)
            .Select(x => x.OutputAudioKey)
            .SingleOrDefaultAsync(ct);
        if (key is null)
            return Results.NotFound(new { code = "audio_not_found" });
        var audio = await store.GetAsync(key, ct);
        return Results.File(audio.Bytes, audio.ContentType);
    }
);
app.MapPost(
    "/api/voice/sessions/{id:guid}/turns",
    async (
        Guid id,
        TurnRequest request,
        VoiceSessionService service,
        IHttpClientFactory clients,
        IObjectStore store,
        AppDbContext db,
        CancellationToken ct
    ) =>
    {
        try
        {
            var (turn, proposalId) = await service.SubmitTextAsync(id, request, ct);
            using var synthesis = await clients
                .CreateClient("ai")
                .PostAsJsonAsync(
                    "internal/speech/synthesize",
                    new SynthesisRequestDto(turn.Reply),
                    ct
                );
            var speech = synthesis.IsSuccessStatusCode
                ? await synthesis.Content.ReadFromJsonAsync<SynthesisDto>(ct)
                : null;
            if (speech is not null)
            {
                turn.OutputAudioKey = $"audio/output/{id}/{turn.Id}.mp3";
                await using var output = new MemoryStream(
                    Convert.FromBase64String(speech.AudioBase64)
                );
                await store.PutAsync(turn.OutputAudioKey, output, speech.AudioContentType, ct);
                await db.SaveChangesAsync(ct);
            }
            return Results.Accepted(
                $"/api/voice/sessions/{id}",
                new VoiceReplyDto(
                    turn.Reply,
                    turn.Transcript,
                    proposalId,
                    speech?.AudioBase64,
                    speech?.AudioContentType
                )
            );
        }
        catch (KeyNotFoundException e)
        {
            return Results.NotFound(new { code = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Results.Json(
                new { code = e.Message },
                statusCode: e.Message is "ai_not_configured" ? 503 : 400
            );
        }
    }
);
app.MapPost(
    "/api/voice/sessions/{id:guid}/turns/audio",
    async (
        Guid id,
        HttpRequest request,
        VoiceSessionService service,
        IHttpClientFactory clients,
        IObjectStore store,
        AppDbContext db,
        CancellationToken ct
    ) =>
    {
        if (!request.HasFormContentType)
            return Results.BadRequest(new { code = "audio_required" });
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("audio");
        var requestId = form["requestId"].ToString();
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            return Results.BadRequest(new { code = "invalid_audio" });
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 100)
            return Results.BadRequest(new { code = "invalid_turn" });
        if (!await db.VoiceSessions.AnyAsync(x => x.Id == id, ct))
            return Results.NotFound(new { code = "session_not_found" });
        using var content = new MultipartFormDataContent();
        await using var rawAudio = new MemoryStream();
        await file.CopyToAsync(rawAudio, ct);
        var inputKey = $"audio/input/{id}/{Guid.NewGuid()}.webm";
        rawAudio.Position = 0;
        await store.PutAsync(
            inputKey,
            rawAudio,
            file.ContentType is { Length: > 0 } mime ? mime : "audio/webm",
            ct
        );
        var audio = new ByteArrayContent(rawAudio.ToArray());
        audio.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
            file.ContentType is { Length: > 0 } type ? type : "audio/webm"
        );
        content.Add(audio, "audio", file.FileName);
        using var transcription = await clients
            .CreateClient("ai")
            .PostAsync("internal/speech/transcribe", content, ct);
        if (!transcription.IsSuccessStatusCode)
            return Results.Json(
                new { code = "speech_transcription_failed" },
                statusCode: (int)transcription.StatusCode
            );
        var transcript = await transcription.Content.ReadFromJsonAsync<TranscriptDto>(ct);
        if (transcript is null)
            return Results.BadRequest(new { code = "empty_transcript" });
        try
        {
            var (turn, proposalId) = await service.SubmitTextAsync(
                id,
                new TurnRequest(requestId, transcript.Transcript),
                ct
            );
            turn.InputAudioKey = inputKey;
            using var synthesis = await clients
                .CreateClient("ai")
                .PostAsJsonAsync(
                    "internal/speech/synthesize",
                    new SynthesisRequestDto(turn.Reply),
                    ct
                );
            var speech = synthesis.IsSuccessStatusCode
                ? await synthesis.Content.ReadFromJsonAsync<SynthesisDto>(ct)
                : null;
            if (speech is not null)
            {
                turn.OutputAudioKey = $"audio/output/{id}/{turn.Id}.mp3";
                await using var output = new MemoryStream(
                    Convert.FromBase64String(speech.AudioBase64)
                );
                await store.PutAsync(turn.OutputAudioKey, output, speech.AudioContentType, ct);
            }
            await db.SaveChangesAsync(ct);
            return Results.Accepted(
                $"/api/voice/sessions/{id}",
                new VoiceReplyDto(
                    turn.Reply,
                    transcript.Transcript,
                    proposalId,
                    speech?.AudioBase64,
                    speech?.AudioContentType
                )
            );
        }
        catch (KeyNotFoundException e)
        {
            return Results.NotFound(new { code = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Results.Json(
                new { code = e.Message },
                statusCode: e.Message == "ai_not_configured" ? 503 : 400
            );
        }
    }
);
app.MapPost(
    "/api/voice/proposals/{id:guid}/confirm",
    async (
        Guid id,
        ConfirmProposalRequest request,
        VoiceSessionService service,
        CancellationToken ct
    ) =>
    {
        try
        {
            return Results.Ok(await service.ConfirmAsync(id, request, ct));
        }
        catch (KeyNotFoundException e)
        {
            return Results.NotFound(new { code = e.Message });
        }
        catch (InvalidOperationException e)
        {
            return Results.Conflict(new { code = e.Message });
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { code = "slot_unavailable" });
        }
        catch (PostgresException exception)
            when (exception.SqlState
                    is PostgresErrorCodes.UniqueViolation
                        or PostgresErrorCodes.SerializationFailure
            )
        {
            return Results.Conflict(new { code = "slot_unavailable" });
        }
    }
);
app.MapPost(
    "/api/voice/proposals/{id:guid}/reject",
    async (Guid id, AppDbContext db, CancellationToken ct) =>
    {
        var proposal = await db.BookingProposals.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (proposal is null)
            return Results.NotFound(new { code = "proposal_not_found" });
        if (proposal.State == "Draft")
        {
            proposal.State = "Rejected";
            proposal.Revision++;
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(new { proposal.Id, proposal.State });
    }
);
app.Run();

static async Task SeedSlotsAsync(AppDbContext db)
{
    if (await db.AvailabilitySlots.AnyAsync())
        return;
    var start = DateTimeOffset.UtcNow.AddDays(1);
    var slots = Enumerable
        .Range(0, 10)
        .Select(i =>
        {
            var next = start.AddDays(i / 4).Date.AddHours(9 + (i % 4) * 2);
            return new VoiceAI.Domain.Entities.AvailabilitySlot
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}"),
                StartUtc = next,
                EndUtc = next.AddMinutes(30),
                ResourceLabel = "Consultation",
            };
        });
    db.AvailabilitySlots.AddRange(slots);
    await db.SaveChangesAsync();
}

public sealed record TranscriptDto(string Transcript);

public sealed record SynthesisRequestDto(string Text);

public sealed record SynthesisDto(string AudioBase64, string AudioContentType);
