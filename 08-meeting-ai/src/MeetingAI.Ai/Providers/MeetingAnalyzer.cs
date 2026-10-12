using System.Net.Http.Json;
using MeetingAI.Ai.Contracts;
using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 51L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 50L * 1024 * 1024);
builder.Services.AddHttpClient();
var app = builder.Build();
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", provider = builder.Configuration["AI_PROVIDER"] ?? "demo" }));
app.MapPost("/internal/meetings/analyze-audio", async (HttpRequest request, IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
{
    var speechEndpoint = config["SPEECH_ENDPOINT"]; var speechKey = config["SPEECH_API_KEY"];
    if (!Uri.TryCreate(speechEndpoint, UriKind.Absolute, out var speechUri) || string.IsNullOrWhiteSpace(speechKey) || speechKey.StartsWith("replace-", StringComparison.OrdinalIgnoreCase)) return Results.Problem("Speech adapter is not configured", statusCode: 503);
    var formData = await request.ReadFormAsync(ct); var file = formData.Files.GetFile("audio"); if (file is null) return Results.BadRequest(new { error = "audio_required" });
    using var speechForm = new MultipartFormDataContent(); var audioContent = new StreamContent(file.OpenReadStream()); audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream"); speechForm.Add(audioContent, "audio", Path.GetFileName(file.FileName)); speechForm.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(new { locales = new[] { config["SPEECH_LOCALE"] ?? "ro-RO" }, diarization = new { enabled = true, maxSpeakers = 8 } })), "definition");
    var http = clients.CreateClient(); http.BaseAddress = speechUri; http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);
    using var speechResponse = await http.PostAsync("speechtotext/transcriptions:transcribe?api-version=2025-10-15", speechForm, ct); if (!speechResponse.IsSuccessStatusCode) return Results.Problem("Speech transcription provider rejected the request", statusCode: (int)speechResponse.StatusCode);
    using var speechJson = System.Text.Json.JsonDocument.Parse(await speechResponse.Content.ReadAsStringAsync(ct)); var root = speechJson.RootElement;
    var phraseArray = root.TryGetProperty("phrases", out var phrases) ? phrases : root.TryGetProperty("combinedPhrases", out var combined) ? combined : default;
    var segments = new List<AiSegment>(); if (phraseArray.ValueKind == System.Text.Json.JsonValueKind.Array)
    {
        foreach (var phrase in phraseArray.EnumerateArray())
        {
            string? text = phrase.TryGetProperty("text", out var tx) ? tx.GetString() : null; if (string.IsNullOrWhiteSpace(text)) continue;
            long start = ReadLong(phrase, "offsetMilliseconds", 0); long duration = ReadLong(phrase, "durationMilliseconds", 1000); string sid = phrase.TryGetProperty("speaker", out var sp) ? sp.ToString() : phrase.TryGetProperty("speakerId", out var sp2) ? sp2.ToString() : "speaker-1";
            segments.Add(new AiSegment(Guid.NewGuid().ToString(), start, start + Math.Max(duration, 1), sid, text));
        }
    }
    if (segments.Count == 0) return Results.Problem("Speech provider returned no timestamped phrases", statusCode: 502);
    var endpoint = config["FOUNDRY_OPENAI_ENDPOINT"]; var model = config["CHAT_DEPLOYMENT"]; var foundryKey = config["FOUNDRY_API_KEY"];
    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var foundryUri) || string.IsNullOrWhiteSpace(foundryKey) || foundryKey.StartsWith("replace-", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(model)) return Results.Problem("Foundry model is not configured", statusCode: 503);
    using var foundry = new HttpClient { BaseAddress = foundryUri, Timeout = TimeSpan.FromSeconds(120) }; foundry.DefaultRequestHeaders.Add("api-key", foundryKey);
    var transcript = string.Join("\n", segments.Select(s => $"{s.Id} [{s.StartMs}-{s.EndMs}ms] {s.SpeakerId}: {s.Text}")); var prompt = $"Summarize this meeting transcript and extract decisions and actionItems. Return JSON exactly as {{summary:string,decisions:[{{text:string,segmentIds:[string]}}],actionItems:[{{text:string,assigneeLabel:string|null,dueText:string|null,segmentIds:[string]}}]}}. Only use provided segment IDs. Leave assignee and due date null unless explicitly stated. Transcript:\n{transcript}";
    using var modelResponse = await foundry.PostAsJsonAsync("chat/completions", new { model, response_format = new { type = "json_object" }, messages = new[] { new { role = "user", content = prompt } } }, ct); if (!modelResponse.IsSuccessStatusCode) return Results.Problem("Foundry analysis request failed", statusCode: (int)modelResponse.StatusCode);
    using var modelJson = System.Text.Json.JsonDocument.Parse(await modelResponse.Content.ReadAsStringAsync(ct)); var content = modelJson.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}"; using var resultJson = System.Text.Json.JsonDocument.Parse(content); var result = resultJson.RootElement;
    var allowed = segments.Select(x => x.Id).ToHashSet(StringComparer.Ordinal); string[] ReadIds(System.Text.Json.JsonElement e) => e.TryGetProperty("segmentIds", out var ids) && ids.ValueKind == System.Text.Json.JsonValueKind.Array ? ids.EnumerateArray().Select(x => x.GetString() ?? "").Where(allowed.Contains).Distinct().ToArray() : [];
    var decisions = result.TryGetProperty("decisions", out var ds) && ds.ValueKind == System.Text.Json.JsonValueKind.Array ? ds.EnumerateArray().Where(d => ReadIds(d).Length > 0).Select(d => new AiDecision(d.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "", ReadIds(d))).ToArray() : [];
    var actions = result.TryGetProperty("actionItems", out var acts) && acts.ValueKind == System.Text.Json.JsonValueKind.Array ? acts.EnumerateArray().Where(a => ReadIds(a).Length > 0).Select(a => new AiAction(a.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "", NullableString(a, "assigneeLabel"), NullableString(a, "dueText"), ReadIds(a))).ToArray() : [];
    return Results.Ok(new AnalyzeResponse(result.TryGetProperty("summary", out var summary) ? summary.GetString() ?? "" : "", segments.ToArray(), decisions, actions, "microsoft-foundry"));
});
static long ReadLong(System.Text.Json.JsonElement element, string property, long fallback) => element.TryGetProperty(property, out var value) && value.TryGetInt64(out var number) ? number : fallback;
static string? NullableString(System.Text.Json.JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : null;
app.MapPost("/internal/meetings/analyze", async ([FromBody] AnalyzeRequest request, IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
{
    var endpoint = config["FOUNDRY_OPENAI_ENDPOINT"];
    var deployment = config["CHAT_DEPLOYMENT"];
    var apiKey = config["FOUNDRY_API_KEY"];
    if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(apiKey) && !apiKey.StartsWith("replace-", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(deployment))
    {
        var client = clients.CreateClient(); client.BaseAddress = uri;
        client.DefaultRequestHeaders.Add("api-key", apiKey);
        var prompt = "Return JSON with summary, segments [{id,startMs,endMs,speakerId,text}], decisions [{text,segmentIds}], actionItems [{text,assigneeLabel,dueText,segmentIds}]. Only include facts stated in transcript. Keep assigneeLabel and dueText null unless explicitly stated. Transcript: " + (request.TranscriptText ?? "");
        using var response = await client.PostAsJsonAsync($"chat/completions", new { model = deployment, response_format = new { type = "json_object" }, messages = new[] { new { role = "user", content = prompt } } }, ct);
        if (!response.IsSuccessStatusCode) return Results.Problem("Foundry request failed", statusCode: (int)response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        return Results.Content(content, "application/json");
    }
    var s1 = new AiSegment("segment-1", 0, 2400, "speaker-1", "Folosim PostgreSQL.");
    var s2 = new AiSegment("segment-2", 2600, 5100, "speaker-2", "Pregătesc schema.");
    return Results.Ok(new AnalyzeResponse("Echipa a decis să folosească PostgreSQL și să pregătească schema bazei de date.", [s1, s2],
        [new AiDecision("Se va folosi PostgreSQL.", [s1.Id])],
        [new AiAction("Pregătirea schemei bazei de date.", null, null, [s2.Id])], "demo"));
});
app.Run();
