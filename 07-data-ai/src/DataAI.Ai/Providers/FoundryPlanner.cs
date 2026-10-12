using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DataAI.Domain.Contracts.AiGateway;

namespace DataAI.Ai.Providers;

public sealed class FoundryPlanner(HttpClient http, IConfiguration configuration)
{
    private readonly string endpoint = configuration["Foundry:OpenAiEndpoint"] ?? "";
    private readonly string key = configuration["Foundry:ApiKey"] ?? "";
    private readonly string deployment = configuration["Foundry:ChatDeployment"] ?? "dataai-chat";
    public bool IsConfigured => Uri.TryCreate(endpoint, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(key) && !key.Contains("placeholder", StringComparison.OrdinalIgnoreCase) && !key.Contains("replace", StringComparison.OrdinalIgnoreCase);

    public async Task<PlanResponse> PlanAsync(PlanRequest request, CancellationToken ct)
    {
        if (!IsConfigured) return LocalPlan(request);
        var prompt = $"Convert the user question into a safe aggregate specification. Only metric revenue or quantity and groupBy category or day. Use only listed categories. Default to the provided entire date range and currency unless a narrower range is requested. Dates are ISO. Question: {request.Question}\nCategories: {string.Join(", ", request.Categories)}\nDefault range: {request.From:yyyy-MM-dd} through but excluding {request.ToExclusive:yyyy-MM-dd}. Currency: {request.Currency}";
        using var doc = await SendResponsesAsync(prompt, "aggregate_plan", """{"type":"object","properties":{"metric":{"type":"string","enum":["revenue","quantity"]},"groupBy":{"type":"string","enum":["category","day"]},"from":{"type":"string","format":"date"},"toExclusive":{"type":"string","format":"date"},"currency":{"type":"string","pattern":"^[A-Z]{3}$"}},"required":["metric","groupBy","from","toExclusive","currency"],"additionalProperties":false}""", ct);
        var plan = JsonSerializer.Deserialize<PlanResponse>(OutputText(doc), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("foundry_invalid_plan");
        return plan with { UsedFoundry = true };
    }

    public async Task<ExplainResponse> ExplainAsync(ExplainRequest request, CancellationToken ct)
    {
        if (!IsConfigured) return new ExplainResponse($"{(request.Metric == "revenue" ? "Revenue" : "Quantity")} by {request.GroupBy}: total {request.Total:0.####}{(request.Metric == "revenue" ? $" {request.Currency}" : " units")}. Values and chart were calculated by the application from dataset rows.", false);
        var data = JsonSerializer.Serialize(request.Rows);
        var prompt = $"Explain the following verified aggregate in one concise sentence. Do not change or recalculate values. Question: {request.Question}. Metric: {request.Metric}; group: {request.GroupBy}; total: {request.Total} {request.Currency}; rows: {data}";
        using var doc = await SendResponsesAsync(prompt, "aggregate_explanation", """{"type":"object","properties":{"explanation":{"type":"string"}},"required":["explanation"],"additionalProperties":false}""", ct);
        var parsed = JsonSerializer.Deserialize<ExplainResponse>(OutputText(doc), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new ExplainResponse(parsed?.Explanation ?? "Analysis complete.", true);
    }

    private async Task<JsonDocument> SendResponsesAsync(string prompt, string schemaName, string schema, CancellationToken ct)
    {
        var baseUrl = endpoint.TrimEnd('/') + "/";
        var url = new Uri(new Uri(baseUrl), "responses");
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("api-key", key);
        request.Content = JsonContent.Create(new { model = deployment, input = prompt, store = false, text = new { format = new { type = "json_schema", name = schemaName, strict = true, schema = JsonDocument.Parse(schema).RootElement } } });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Foundry request failed with HTTP {(int)response.StatusCode}");
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(body, cancellationToken: ct);
    }

    private static string OutputText(JsonDocument doc) => doc.RootElement.GetProperty("output").EnumerateArray().SelectMany(x => x.TryGetProperty("content", out var c) ? c.EnumerateArray() : []).Where(x => x.TryGetProperty("text", out _)).Select(x => x.GetProperty("text").GetString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? throw new InvalidOperationException("foundry_missing_output");

    private static PlanResponse LocalPlan(PlanRequest request)
    {
        var q = request.Question.ToLowerInvariant();
        return new PlanResponse(q.Contains("quantity") || q.Contains("cantitat") || q.Contains("bucăț") || q.Contains("bucat") ? "quantity" : "revenue", q.Contains("per day") || q.Contains("by day") || q.Contains("zi") || q.Contains("zilnic") ? "day" : "category", request.From, request.ToExclusive, request.Currency, false);
    }
}
