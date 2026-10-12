using System.Text.Json;

namespace VideoSearchAI.Ai.Providers;

public static class ProviderHttp
{
    public static async Task RequireSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        // Never include the provider body, signed operation URL, or credentials in errors.
        await Task.CompletedTask;
        throw new ProviderException(
            $"provider_http_{(int)response.StatusCode}",
            (int)response.StatusCode == 429 ? 429
                : (int)response.StatusCode >= 500 ? 503
                : 502,
            response.Headers.RetryAfter?.Delta
        );
    }

    public static async Task<JsonDocument> JsonAsync(HttpResponseMessage r, CancellationToken ct)
    {
        await RequireSuccessAsync(r, ct);
        await using var stream = await r.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
