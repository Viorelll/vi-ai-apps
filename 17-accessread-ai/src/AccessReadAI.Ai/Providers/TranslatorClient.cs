using System.Net.Http.Json;
using AccessReadAI.Ai.Models;
using AccessReadAI.Contracts;
using Microsoft.Extensions.Options;

namespace AccessReadAI.Ai.Providers;

public sealed class TranslatorClient(HttpClient http, IOptions<ProviderOptions> options)
{
    public async Task<string> TranslateAsync(
        string text,
        string from,
        string to,
        CancellationToken ct
    )
    {
        LanguageCatalog.Validate(from);
        LanguageCatalog.Validate(to);
        if (from == to)
            return text;
        var config = options.Value;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(
                new Uri(config.TranslatorEndpoint),
                $"translate?api-version=3.0&from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&textType=plain"
            )
        );
        request.Headers.Add("Ocp-Apim-Subscription-Key", config.TranslatorKey);
        request.Headers.Add("Ocp-Apim-Subscription-Region", config.TranslatorRegion);
        request.Content = JsonContent.Create(
            new[] { new Dictionary<string, string> { { "Text", text } } }
        );
        using var response = await http.SendAsync(request, ct);
        using var result = await ProviderHttp.ReadAsync(response, ct);
        return result.RootElement[0].GetProperty("translations")[0].GetProperty("text").GetString()
            ?? throw new ProviderFault("empty_translation");
    }
}
