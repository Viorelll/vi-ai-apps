using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;

namespace VideoSearchAI.Ai.Providers.Foundry;

public sealed class FoundryV1Client(HttpClient http, IOptions<AiOptions> options)
{
    private readonly AiOptions o = options.Value;

    public async Task<JsonDocument> PostAsync(string path, object payload, CancellationToken ct)
    {
        if (path is not "responses" and not "embeddings")
            throw new ProviderException("invalid_foundry_path", 400);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(o.OpenAiEndpoint), path)
        );
        request.Headers.Add("api-key", o.ApiKey);
        request.Content = JsonContent.Create(payload);
        using var response = await http.SendAsync(request, ct);
        return await ProviderHttp.JsonAsync(response, ct);
    }

    public async Task<float[][]> EmbedAsync(string[] input, CancellationToken ct)
    {
        if (input.Length == 0 || input.Length > 32 || input.Any(x => x.Length > 16000))
            throw new ProviderException("embedding_input_limit", 400);
        using var result = await PostAsync(
            "embeddings",
            new
            {
                model = o.EmbeddingDeployment,
                input,
                dimensions = o.EmbeddingDimensions,
                encoding_format = "float",
            },
            ct
        );
        var data = result
            .RootElement.GetProperty("data")
            .EnumerateArray()
            .OrderBy(x => x.GetProperty("index").GetInt32())
            .ToArray();
        if (
            data.Length != input.Length
            || data.Where((x, i) => x.GetProperty("index").GetInt32() != i).Any()
        )
            throw new ProviderException("embedding_contract_mismatch");
        var vectors = data.Select(x =>
                x.GetProperty("embedding").EnumerateArray().Select(n => n.GetSingle()).ToArray()
            )
            .ToArray();
        if (vectors.Any(v => v.Length != o.EmbeddingDimensions || v.Any(n => !float.IsFinite(n))))
            throw new ProviderException("embedding_contract_mismatch");
        return vectors;
    }
}
