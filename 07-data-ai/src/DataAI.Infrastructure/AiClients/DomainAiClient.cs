using System.Net.Http.Json;
using DataAI.Domain.Contracts.AiGateway;
using DataAI.Application.Abstractions;
using DataAI.Domain.Contracts;

namespace DataAI.Infrastructure.AiClients;

public sealed class DomainAiClient(HttpClient http) : IAggregatePlanner
{
    private bool configured;
    public bool IsConfigured => configured;

    public async Task<AggregateSpec> PlanAsync(string question, IReadOnlyCollection<string> categories, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("plan", new PlanRequest(question, categories, DateOnly.MinValue, new DateOnly(9999, 12, 31), "EUR"), ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PlanResponse>(cancellationToken: ct) ?? throw new InvalidOperationException("ai_gateway_empty_response");
        configured = result.UsedFoundry;
        return new AggregateSpec(result.Metric, result.GroupBy, result.From, result.ToExclusive, result.Currency);
    }

    public async Task<string> ExplainAsync(string question, AggregateSpec spec, IReadOnlyList<AggregateBucket> rows, decimal total, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("explain", new ExplainRequest(question, spec.Metric, spec.GroupBy, spec.Currency, rows.ToArray(), total), ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ExplainResponse>(cancellationToken: ct) ?? throw new InvalidOperationException("ai_gateway_empty_response");
        configured = result.UsedFoundry;
        return result.Explanation;
    }
}
