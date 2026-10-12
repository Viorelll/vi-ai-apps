using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Ai.Providers.Foundry;
using VideoSearchAI.Contracts.Dto;

namespace VideoSearchAI.Ai.Providers.Search;

public sealed class AzureSearchProvider(
    HttpClient http,
    FoundryV1Client foundry,
    IOptions<AiOptions> options
)
{
    private readonly AiOptions o = options.Value;

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken ct
    )
    {
        using var request = new HttpRequestMessage(
            method,
            new Uri(new Uri(o.SearchEndpoint), path + "?api-version=2024-07-01")
        );
        request.Headers.Add("api-key", o.SearchKey);
        if (body != null)
            request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        return await ProviderHttp.JsonAsync(response, ct);
    }

    private string IndexPath => $"indexes/{Uri.EscapeDataString(o.SearchIndex)}";

    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        // GET existing schema first: never replace a user's existing index with incompatible vector dimensions.
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(new Uri(o.SearchEndpoint), IndexPath + "?api-version=2024-07-01")
        );
        request.Headers.Add("api-key", o.SearchKey);
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            using var existing = await ProviderHttp.JsonAsync(response, ct);
            var vector = existing
                .RootElement.GetProperty("fields")
                .EnumerateArray()
                .Single(x => x.GetProperty("name").GetString() == "contentVector");
            if (vector.GetProperty("dimensions").GetInt32() != o.EmbeddingDimensions)
                throw new ProviderException("search_dimension_mismatch");
            return;
        }
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            await ProviderHttp.RequireSuccessAsync(response, ct);
            return;
        }
        object[] fields =
        [
            new
            {
                name = "id",
                type = "Edm.String",
                key = true,
                filterable = true,
            },
            new
            {
                name = "documentId",
                type = "Edm.String",
                filterable = true,
            },
            new
            {
                name = "generation",
                type = "Edm.Int32",
                filterable = true,
            },
            new
            {
                name = "content",
                type = "Edm.String",
                searchable = true,
            },
            new
            {
                name = "title",
                type = "Edm.String",
                searchable = true,
            },
            new
            {
                name = "startMs",
                type = "Edm.Int64",
                filterable = true,
            },
            new
            {
                name = "endMs",
                type = "Edm.Int64",
                filterable = true,
            },
            new
            {
                name = "contentVector",
                type = "Collection(Edm.Single)",
                searchable = true,
                retrievable = false,
                dimensions = o.EmbeddingDimensions,
                vectorSearchProfile = "temporal-vector",
            },
        ];
        using var result = await SendAsync(
            HttpMethod.Put,
            IndexPath,
            new
            {
                name = o.SearchIndex,
                fields,
                vectorSearch = new
                {
                    algorithms = new[]
                    {
                        new
                        {
                            name = "temporal-hnsw",
                            kind = "hnsw",
                            hnswParameters = new { metric = "cosine" },
                        },
                    },
                    profiles = new[]
                    {
                        new { name = "temporal-vector", algorithm = "temporal-hnsw" },
                    },
                },
            },
            ct
        );
    }

    public async Task UpsertAsync(IndexRequest r, CancellationToken ct)
    {
        await EnsureIndexAsync(ct);
        foreach (var batch in r.Segments.Chunk(32))
        {
            var vectors = await foundry.EmbedAsync(
                batch.Select(x => x.Transcript + "\n" + x.VisualText).ToArray(),
                ct
            );
            var docs = batch
                .Select(
                    (s, i) =>
                        new Dictionary<string, object?>
                        {
                            ["@search.action"] = "mergeOrUpload",
                            ["id"] = s.Id.ToString(),
                            ["documentId"] = r.DocumentId.ToString(),
                            ["generation"] = r.Generation,
                            ["title"] = r.Title,
                            ["content"] = s.Transcript + "\n" + s.VisualText,
                            ["startMs"] = s.StartMs,
                            ["endMs"] = s.EndMs,
                            ["contentVector"] = vectors[i],
                        }
                )
                .ToArray();
            using var result = await SendAsync(
                HttpMethod.Post,
                IndexPath + "/docs/index",
                new { value = docs },
                ct
            );
            var items = result.RootElement.GetProperty("value").EnumerateArray().ToArray();
            if (
                items.Length != docs.Length
                || items.Any(x => !x.GetProperty("status").GetBoolean())
            )
                throw new ProviderException("partial_index_failure", 503);
        }
    }

    public async Task<RetrievalHit[]> RetrieveAsync(RetrieveRequest r, CancellationToken ct)
    {
        if (r.DocumentIds.Length == 0)
            return [];
        var vectors = await foundry.EmbedAsync([r.Question], ct);
        var filter = string.Join(
            " or ",
            r.DocumentIds.Distinct()
                .Select(id => $"(documentId eq '{id}' and generation eq {r.Generations[id]})")
        );
        using var result = await SendAsync(
            HttpMethod.Post,
            IndexPath + "/docs/search",
            new
            {
                search = r.Question,
                filter,
                select = "id,documentId,generation",
                top = 50,
                vectorFilterMode = "preFilter",
                vectorQueries = new[]
                {
                    new
                    {
                        kind = "vector",
                        vector = vectors[0],
                        fields = "contentVector",
                        k = 50,
                    },
                },
            },
            ct
        );
        return result
            .RootElement.GetProperty("value")
            .EnumerateArray()
            .Select(x => new RetrievalHit(
                Guid.Parse(x.GetProperty("id").GetString()!),
                Guid.Parse(x.GetProperty("documentId").GetString()!),
                x.GetProperty("generation").GetInt32(),
                x.GetProperty("@search.score").GetDouble()
            ))
            .ToArray();
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken ct)
    {
        // Delete in bounded batches, repeat until no visible keys remain.
        for (var round = 0; round < 100; round++)
        {
            using var page = await SendAsync(
                HttpMethod.Post,
                IndexPath + "/docs/search",
                new
                {
                    search = "*",
                    filter = $"documentId eq '{documentId}'",
                    select = "id",
                    top = 1000,
                },
                ct
            );
            var ids = page
                .RootElement.GetProperty("value")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetString()!)
                .ToArray();
            if (ids.Length == 0)
                return;
            using var result = await SendAsync(
                HttpMethod.Post,
                IndexPath + "/docs/index",
                new
                {
                    value = ids.Select(x => new Dictionary<string, object>
                        {
                            ["@search.action"] = "delete",
                            ["id"] = x,
                        })
                        .ToArray(),
                },
                ct
            );
            if (
                result
                    .RootElement.GetProperty("value")
                    .EnumerateArray()
                    .Any(x => !x.GetProperty("status").GetBoolean())
            )
                throw new ProviderException("partial_delete_failure", 503);
            await Task.Delay(500, ct);
        }
        throw new ProviderException("index_cleanup_pending", 503);
    }
}
