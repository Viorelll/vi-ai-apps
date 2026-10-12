using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Infrastructure.Storage;

namespace VideoSearchAI.Ai.Providers.Search;

public sealed partial class DemoRetrievalProvider(ObjectStore store)
{
    public Task UpsertAsync(IndexRequest r, CancellationToken ct) =>
        store.PutTextAsync(
            $"demo-index/{r.DocumentId:N}/{r.Generation}.json",
            JsonSerializer.Serialize(r),
            ct
        );

    public Task DeleteAsync(Guid id, CancellationToken ct) =>
        store.DeletePrefixAsync($"demo-index/{id:N}/", ct);

    public async Task<RetrievalHit[]> RetrieveAsync(RetrieveRequest r, CancellationToken ct)
    {
        var hits = new List<RetrievalHit>();
        var tokens = Words()
            .Matches(r.Question.ToLowerInvariant())
            .Select(x => x.Value)
            .Where(x => x.Length >= 3)
            .Distinct()
            .ToArray();
        foreach (var id in r.DocumentIds.Distinct())
        {
            // Publication is controlled by backend generations; scan bounded known index objects through S3.
            foreach (var generation in new[] { r.Generations[id] })
            {
                IndexRequest index;
                try
                {
                    index = JsonSerializer.Deserialize<IndexRequest>(
                        await store.ReadTextAsync($"demo-index/{id:N}/{generation}.json", ct)
                    )!;
                }
                catch (AmazonS3Exception e)
                    when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    break;
                }
                foreach (var s in index.Segments)
                {
                    var text = (s.Transcript + " " + s.VisualText).ToLowerInvariant();
                    var score = tokens.Count(t => text.Contains(t, StringComparison.Ordinal));
                    if (score > 0)
                        hits.Add(new(s.Id, id, s.Generation, score));
                }
            }
        }
        return hits.OrderByDescending(x => x.Score).Take(50).ToArray();
    }

    [GeneratedRegex(@"[\p{L}\p{N}_]+")]
    private static partial Regex Words();
}
