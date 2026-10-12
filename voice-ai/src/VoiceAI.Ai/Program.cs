using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VoiceAI.Domain.DTOs;

var builder = WebApplication.CreateBuilder(args);
var internalKey = builder.Configuration["Internal:Key"] ?? "replace-internal-key";
builder.Services.AddHttpClient("foundry", client => client.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient("speech", client => client.Timeout = TimeSpan.FromSeconds(60));
var app = builder.Build();
app.Use(
    async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            var supplied = context.Request.Headers["X-Internal-Key"].ToString();
            var expectedBytes = Encoding.UTF8.GetBytes(internalKey);
            var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
            if (
                !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    expectedBytes,
                    suppliedBytes
                )
            )
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await next();
    }
);
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    () =>
        Configured(builder.Configuration)
            ? Results.Ok(new { status = "ok" })
            : Results.Json(new { status = "configuration_required" }, statusCode: 503)
);
app.MapPost(
    "/internal/voice/turn",
    async (
        AiTurnRequest request,
        IConfiguration config,
        IHttpClientFactory clients,
        CancellationToken ct
    ) =>
    {
        if (!Configured(config))
            return Results.Json(new { code = "ai_not_configured" }, statusCode: 503);
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 4000)
            return Results.BadRequest(new { code = "invalid_turn" });
        var slots = JsonSerializer.Serialize(request.AvailableSlots);
        var tools = new JsonArray
        {
            new JsonObject
            {
                ["type"] = "function",
                ["name"] = "get_available_slots",
                ["description"] =
                    "Return the currently available appointment slots for the requested timezone.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject(),
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray(),
                },
                ["strict"] = true,
            },
            new JsonObject
            {
                ["type"] = "function",
                ["name"] = "propose_booking",
                ["description"] =
                    "Prepare a draft proposal for one slot returned by get_available_slots. Never confirm or claim it is booked.",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["slotId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                    },
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("slotId"),
                },
                ["strict"] = true,
            },
        };
        var root = JsonNode.Parse("{}");
        root!["model"] = config["Foundry:Deployment"] ?? "voiceai-chat";
        root["instructions"] =
            "You are VoiceAI, a concise Romanian appointment assistant. Use get_available_slots before proposing. Only propose one slot that exists in its result. Convert the requested time using the supplied IANA timezone and show date, time, and timezone. Ask a clarifying question for ambiguity. Never claim an appointment is confirmed; only the user can confirm the visible proposal in the app. If no suitable slot exists, say so. Do not request personal data.";
        root["input"] = new JsonArray(
            new JsonObject
            {
                ["role"] = "user",
                ["content"] =
                    $"Timezone: {request.TimeZoneId}\nAvailable slot IDs and UTC intervals: {slots}\nUser: {request.Text}",
            }
        );
        root["tools"] = tools;
        Guid? proposedSlot = null;
        string? lastText = null;
        var client = clients.CreateClient("foundry");
        for (var round = 0; round < 3; round++)
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                (config["Foundry:Endpoint"] ?? "").TrimEnd('/') + "/responses"
            );
            message.Headers.Add("api-key", config["Foundry:ApiKey"]);
            message.Content = new StringContent(
                root.ToJsonString(),
                Encoding.UTF8,
                "application/json"
            );
            using var response = await client.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
                return Results.Json(
                    new { code = "foundry_request_failed", status = (int)response.StatusCode },
                    statusCode: 502
                );
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            var output = result?["output"]?.AsArray() ?? [];
            var calls = output
                .Where(x => x?["type"]?.GetValue<string>() == "function_call")
                .ToArray();
            lastText = string.Join(
                "\n",
                output
                    .Where(x => x?["type"]?.GetValue<string>() == "message")
                    .SelectMany(x => x?["content"]?.AsArray() ?? [])
                    .Where(x => x?["type"]?.GetValue<string>() == "output_text")
                    .Select(x => x?["text"]?.GetValue<string>() ?? "")
            );
            if (calls.Length == 0)
                break;
            foreach (var item in output)
                root["input"]!.AsArray().Add(item?.DeepClone());
            foreach (var call in calls)
            {
                var name = call?["name"]?.GetValue<string>();
                var callId = call?["call_id"]?.GetValue<string>();
                var arguments = JsonNode.Parse(call?["arguments"]?.GetValue<string>() ?? "{}");
                string toolOutput;
                if (name == "get_available_slots")
                    toolOutput = slots;
                else if (
                    name == "propose_booking"
                    && Guid.TryParse(arguments?["slotId"]?.GetValue<string>(), out var id)
                    && request.AvailableSlots.Any(x => x.Id == id)
                )
                {
                    proposedSlot = id;
                    toolOutput = JsonSerializer.Serialize(
                        new
                        {
                            accepted = true,
                            note = "Draft only. Explicit user confirmation is still required.",
                        }
                    );
                }
                else
                    toolOutput = "No proposal was created. Choose only a currently available slot.";
                root["input"]!
                    .AsArray()
                    .Add(
                        new JsonObject
                        {
                            ["type"] = "function_call_output",
                            ["call_id"] = callId,
                            ["output"] = toolOutput,
                        }
                    );
            }
        }
        if (string.IsNullOrWhiteSpace(lastText))
            return Results.Json(new { code = "empty_model_response" }, statusCode: 502);
        return Results.Ok(new AiTurnResult(lastText, proposedSlot));
    }
);
app.MapPost(
    "/internal/speech/transcribe",
    async (
        HttpRequest request,
        IConfiguration config,
        IHttpClientFactory clients,
        CancellationToken ct
    ) =>
    {
        if (!SpeechConfigured(config))
            return Results.Json(new { code = "speech_not_configured" }, statusCode: 503);
        if (!request.HasFormContentType)
            return Results.BadRequest(new { code = "audio_required" });
        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("audio");
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            return Results.BadRequest(new { code = "invalid_audio" });
        var speechEndpoint = config["Speech:Endpoint"]!.TrimEnd('/');
        using var content = new MultipartFormDataContent();
        await using var input = new MemoryStream();
        await file.CopyToAsync(input, ct);
        input.Position = 0;
        var startInfo = new ProcessStartInfo(
            "ffmpeg",
            "-hide_banner -loglevel error -i pipe:0 -ac 1 -ar 16000 -f wav pipe:1"
        )
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var decoder = Process.Start(startInfo);
        if (decoder is null)
            return Results.Json(new { code = "audio_decoder_unavailable" }, statusCode: 503);
        await using var wav = new MemoryStream();
        var copyOut = decoder.StandardOutput.BaseStream.CopyToAsync(wav, ct);
        await input.CopyToAsync(decoder.StandardInput.BaseStream, ct);
        decoder.StandardInput.Close();
        await copyOut;
        var decodeError = await decoder.StandardError.ReadToEndAsync(ct);
        await decoder.WaitForExitAsync(ct);
        if (decoder.ExitCode != 0 || wav.Length == 0)
            return Results.BadRequest(
                new
                {
                    code = "unsupported_audio_format",
                    detail = string.IsNullOrWhiteSpace(decodeError)
                        ? "Audio could not be decoded."
                        : "Audio could not be decoded.",
                }
            );
        var audio = new ByteArrayContent(wav.ToArray());
        audio.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        content.Add(audio, "audio", "recording.wav");
        content.Add(
            new StringContent(
                "{\"locales\":[\"ro-RO\",\"en-US\"]}",
                Encoding.UTF8,
                "application/json"
            ),
            "definition"
        );
        using var msg = new HttpRequestMessage(
            HttpMethod.Post,
            speechEndpoint + "/speechtotext/transcriptions:transcribe?api-version=2024-11-15"
        )
        {
            Content = content,
        };
        msg.Headers.Add("Ocp-Apim-Subscription-Key", config["Speech:ApiKey"]);
        using var response = await clients.CreateClient("speech").SendAsync(msg, ct);
        if (!response.IsSuccessStatusCode)
            return Results.Json(
                new { code = "speech_transcription_failed", status = (int)response.StatusCode },
                statusCode: 502
            );
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        var transcript = json
            ?["phrases"]?.AsArray()
            .Select(x => x?["text"]?.GetValue<string>())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return string.IsNullOrWhiteSpace(transcript)
            ? Results.BadRequest(new { code = "empty_transcript" })
            : Results.Ok(new { transcript });
    }
);
app.MapPost(
    "/internal/speech/synthesize",
    async (
        SynthesisRequest request,
        IConfiguration config,
        IHttpClientFactory clients,
        CancellationToken ct
    ) =>
    {
        if (!SpeechConfigured(config))
            return Results.Json(new { code = "speech_not_configured" }, statusCode: 503);
        var endpoint = config["Speech:Endpoint"]!.TrimEnd('/');
        var region = config["Speech:Region"] ?? "eastus";
        var url = endpoint.Contains(".tts.speech.microsoft.com", StringComparison.OrdinalIgnoreCase)
            ? endpoint + "/cognitiveservices/v1"
            : $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
        var ssml =
            $"<speak version=\"1.0\" xml:lang=\"ro-RO\"><voice name=\"ro-RO-AlinaNeural\">{System.Security.SecurityElement.Escape(request.Text)}</voice></speak>";
        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml"),
        };
        message.Headers.Add("Ocp-Apim-Subscription-Key", config["Speech:ApiKey"]);
        message.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        using var response = await clients.CreateClient("speech").SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
            return Results.Json(
                new { code = "speech_synthesis_failed", status = (int)response.StatusCode },
                statusCode: 502
            );
        return Results.Ok(
            new
            {
                audioBase64 = Convert.ToBase64String(
                    await response.Content.ReadAsByteArrayAsync(ct)
                ),
                audioContentType = "audio/mpeg",
            }
        );
    }
);
app.Run();

static bool Configured(IConfiguration config) =>
    !string.IsNullOrWhiteSpace(config["Foundry:Endpoint"])
    && !string.IsNullOrWhiteSpace(config["Foundry:ApiKey"])
    && !config["Foundry:ApiKey"]!.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);
static bool SpeechConfigured(IConfiguration config) =>
    !string.IsNullOrWhiteSpace(config["Speech:Endpoint"])
    && !string.IsNullOrWhiteSpace(config["Speech:ApiKey"])
    && !config["Speech:ApiKey"]!.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);

public sealed record SynthesisRequest(string Text);
