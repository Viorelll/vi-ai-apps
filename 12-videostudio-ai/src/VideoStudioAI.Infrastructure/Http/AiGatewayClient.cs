using System.Net.Http.Json;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Infrastructure.Storage;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Infrastructure.Http;

public sealed class AiGatewayClient(HttpClient http) : IAiGateway
{
    public Task<Capabilities> CapabilitiesAsync(CancellationToken ct) =>
        GetAsync<Capabilities>("internal/capabilities", ct);

    public Task<Storyboard> StoryboardAsync(string brief, CancellationToken ct) =>
        PostAsync<Storyboard>("internal/storyboard", new { brief }, ct);

    public Task<ProviderJob> SubmitAsync(VideoSubmit input, CancellationToken ct) =>
        PostAsync<ProviderJob>("internal/videos", input, ct);

    public Task<ProviderJob> PollAsync(string id, CancellationToken ct) =>
        GetAsync<ProviderJob>($"internal/videos/{Uri.EscapeDataString(id)}", ct);

    public Task<ExtractResult> ExtractAsync(ExtractRequest input, CancellationToken ct) =>
        PostAsync<ExtractResult>("internal/extract", input, ct);

    public Task<GroundedAnswer> AnswerAsync(GroundedRequest input, CancellationToken ct) =>
        PostAsync<GroundedAnswer>("internal/chat", input, ct);

    public async Task<StoredObject> DownloadAsync(string id, CancellationToken ct)
    {
        using var r = await http.GetAsync(
            $"internal/videos/{Uri.EscapeDataString(id)}/content",
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        await CheckAsync(r, ct);
        await using var s = await r.Content.ReadAsStreamAsync(ct);
        return await TemporaryFiles.SpoolAsync(s, "video/mp4", ct);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var r = await http.GetAsync(path, ct);
        await CheckAsync(r, ct);
        return await r.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new AppException(502, "gateway_response_invalid", "Empty gateway response.");
    }

    private async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var r = await http.PostAsJsonAsync(path, body, ct);
        await CheckAsync(r, ct);
        return await r.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new AppException(502, "gateway_response_invalid", "Empty gateway response.");
    }

    private static async Task CheckAsync(HttpResponseMessage r, CancellationToken ct)
    {
        if (r.IsSuccessStatusCode)
            return;
        var code = "gateway_failure";
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(
                await r.Content.ReadAsStringAsync(ct)
            );
            if (json.RootElement.TryGetProperty("code", out var c))
                code = c.GetString() ?? code;
        }
        catch (System.Text.Json.JsonException) { }
        throw new AppException(
            (int)r.StatusCode,
            code,
            "The AI gateway could not complete this operation."
        );
    }
}
