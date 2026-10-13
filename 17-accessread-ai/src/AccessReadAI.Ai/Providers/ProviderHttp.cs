using System.Text.Json;
using AccessReadAI.Contracts;

namespace AccessReadAI.Ai.Providers;

public static class ProviderHttp
{
    public static async Task EnsureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        // Provider response bodies may contain document text or credentials and are never logged.
        await Task.CompletedTask;
        var status = (int)response.StatusCode;
        throw new ProviderFault(
            $"provider_http_{status}",
            status is 429 or >= 500,
            (int)(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 0)
        );
    }

    public static async Task<JsonDocument> ReadAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        await EnsureAsync(response, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public static Uri OperationUri(string root, string location)
    {
        var configured = new Uri(root);
        var operation = new Uri(location);
        if (
            operation.Scheme != "https"
            || operation.Host != configured.Host
            || operation.Port != configured.Port
            || !operation.AbsolutePath.StartsWith(
                "/documentintelligence/",
                StringComparison.Ordinal
            )
            || !string.IsNullOrEmpty(operation.UserInfo)
        )
            throw new ProviderFault("unsafe_operation_location");
        return operation;
    }
}
