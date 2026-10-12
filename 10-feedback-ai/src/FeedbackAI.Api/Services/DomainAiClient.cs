using System.Net.Http.Json;
using FeedbackAI.Application.Abstractions;

namespace FeedbackAI.Api.Services;

public sealed class DomainAiClient(HttpClient client) : IClassifier
{
    public async Task<ClassificationOutput> ClassifyAsync(ClassificationInput input, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("internal/classify", new { text = input.RedactedText }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClassificationOutput>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("ai_empty_response");
    }
}
