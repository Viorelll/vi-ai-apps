using System.Net.Http.Json;
using System.Text.Json;
using ImageAI.Ai.Options;
using ImageAI.Contracts.Dto;
using ImageAI.Infrastructure.Validation;
using Microsoft.Extensions.Options;

namespace ImageAI.Ai.Providers;

public sealed class FoundryImageProvider(
    HttpClient http,
    IOptions<ImagesOptions> options,
    FoundryPromptService prompts
) : IImageProvider
{
    private static string? Usage(JsonElement root, string? promptUsage)
    {
        var imageUsage = root.TryGetProperty("usage", out var usage) ? usage.GetRawText() : null;
        if (promptUsage is null)
            return imageUsage;
        return JsonSerializer.Serialize(
            new
            {
                prompt = JsonSerializer.Deserialize<JsonElement>(promptUsage),
                image = imageUsage is null
                    ? (JsonElement?)null
                    : JsonSerializer.Deserialize<JsonElement>(imageUsage),
            }
        );
    }

    public async Task<GatewayResult> GenerateAsync(GatewayRequest input, CancellationToken ct)
    {
        var o = options.Value;
        if (
            o.Endpoint.Contains("RESOURCE", StringComparison.OrdinalIgnoreCase)
            || o.ApiKey.StartsWith("replace-")
            || o.Deployment.StartsWith("replace-")
        )
            throw new ProviderException("provider_not_configured", 503);
        var prepared = await prompts.PrepareAsync(input, ct);
        var prompt = prepared.Prompt;
        var path =
            $"openai/deployments/{Uri.EscapeDataString(o.Deployment)}/images/{(input.ImageBase64 is null ? "generations" : "edits")}?api-version={Uri.EscapeDataString(o.ApiVersion)}";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(o.Endpoint), path)
        );
        request.Headers.Add("api-key", o.ApiKey);
        request.Headers.Add("X-Correlation-Id", input.OperationId.ToString());
        if (input.ImageBase64 is null)
            request.Content = JsonContent.Create(
                new
                {
                    prompt,
                    n = 1,
                    size = input.Size,
                    quality = input.Quality,
                    output_format = "png",
                }
            );
        else
        {
            var multipart = new MultipartFormDataContent();
            var bytes = Convert.FromBase64String(input.ImageBase64);
            var info = ImageValidator.Inspect(bytes);
            var image = new ByteArrayContent(bytes);
            image.Headers.ContentType = new(info.ContentType);
            multipart.Add(
                image,
                "image",
                info.ContentType == "image/png" ? "input.png" : "input.jpg"
            );
            if (input.MaskBase64 is { } encoded)
            {
                var mask = new ByteArrayContent(Convert.FromBase64String(encoded));
                mask.Headers.ContentType = new("image/png");
                multipart.Add(mask, "mask", "mask.png");
            }
            multipart.Add(new StringContent(prompt), "prompt");
            multipart.Add(new StringContent("1"), "n");
            multipart.Add(new StringContent(input.Size), "size");
            multipart.Add(new StringContent(input.Quality), "quality");
            multipart.Add(new StringContent("png"), "output_format");
            request.Content = multipart;
        }
        using var response = await http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            if (status == 400)
            {
                try
                {
                    var errorText = await response.Content.ReadAsStringAsync(ct);
                    if (errorText.Length <= 100_000)
                    {
                        using var error = JsonDocument.Parse(errorText);
                        if (
                            error.RootElement.TryGetProperty("error", out var details)
                            && details.TryGetProperty("code", out var code)
                            && code.GetString()
                                is "content_filter"
                                    or "ResponsibleAIPolicyViolation"
                        )
                            throw new ProviderException("provider_safety_blocked", 400);
                    }
                }
                catch (JsonException) { }
            }
            // Never expose raw provider bodies, endpoint URLs or keys in public errors.
            throw new ProviderException(
                status == 429 ? "provider_rate_limited"
                    : status is 401 or 403 ? "provider_auth_failed"
                    : status == 400 ? "provider_rejected_request"
                    : "provider_operation_uncertain",
                status == 429 ? 429
                    : status >= 500 ? 502
                    : 400,
                (int)Math.Clamp(response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 5, 1, 300)
            );
        }
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (content.Length + count > 28_000_000)
                throw new ProviderException("provider_response_size_limit", 502);
            await content.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        try
        {
            using var json = JsonDocument.Parse(content.ToArray());
            var root = json.RootElement;
            var encoded =
                root.GetProperty("data")[0].GetProperty("b64_json").GetString()
                ?? throw new JsonException();
            if (encoded.Length > 27_000_000)
                throw new ProviderException("provider_response_size_limit", 502);
            var bytes = Convert.FromBase64String(encoded);
            var info = ImageValidator.Inspect(bytes);
            if (info.ContentType != "image/png" || $"{info.Width}x{info.Height}" != input.Size)
                throw new ProviderException("provider_invalid_image", 502);
            return new(
                encoded,
                o.Deployment,
                Usage(root, prepared.UsageJson),
                response.Headers.TryGetValues("x-request-id", out var values)
                    ? values.FirstOrDefault()
                    : null,
                "ProviderManaged",
                prompt
            );
        }
        catch (Exception e)
            when (e
                    is JsonException
                        or IndexOutOfRangeException
                        or FormatException
                        or KeyNotFoundException
                        or InvalidOperationException
                        or ImageAI.Domain.Models.DomainException
            )
        {
            throw new ProviderException("provider_invalid_response", 502);
        }
    }
}
