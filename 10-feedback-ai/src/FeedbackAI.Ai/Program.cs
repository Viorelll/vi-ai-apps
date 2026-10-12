using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("foundry", c => c.Timeout = TimeSpan.FromSeconds(90));
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapPost("/internal/classify", async (ClassifyRequest request, IHttpClientFactory clients, IConfiguration config, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 12000) return Results.BadRequest(new { code = "invalid_text" });
    if (!string.Equals(config["AI_MODE"], "Foundry", StringComparison.OrdinalIgnoreCase)) return Results.Ok(DemoClassifier.Classify(request.Text));
    var endpoint = config["FOUNDRY_OPENAI_ENDPOINT"]?.TrimEnd('/') + "/chat/completions";
    var key = config["FOUNDRY_API_KEY"];
    var deployment = config["CHAT_DEPLOYMENT"];
    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment)) return Results.Problem("Foundry configuration is incomplete", statusCode: 503);
    using var http = new HttpRequestMessage(HttpMethod.Post, endpoint);
    http.Headers.Add("api-key", key);
    http.Content = JsonContent.Create(new
    {
        model = deployment,
        temperature = 0,
        response_format = new { type = "json_object" },
        messages = new object[] {
        new { role = "system", content = "Classify customer feedback. Return only JSON with topic (delivery|product|information|other|unknown), sentiment (positive|neutral|negative|unknown), urgency (normal|high|unknown), reason. Negative sentiment alone does not mean high urgency. Never infer or repeat personal data." },
        new { role = "user", content = request.Text }
    }
    });
    using var response = await clients.CreateClient("foundry").SendAsync(http, ct);
    if (!response.IsSuccessStatusCode) return Results.Problem("Foundry request failed", statusCode: (int)response.StatusCode);
    using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    var content = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    var result = JsonSerializer.Deserialize<ClassificationResult>(content ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return result is null ? Results.Problem("Foundry returned invalid JSON", statusCode: 502) : Results.Ok(result with { Provider = "Microsoft Foundry", ModelVersion = deployment });
});
app.Run();

public sealed record ClassifyRequest(string Text);
public sealed record ClassificationResult([property: JsonPropertyName("topic")] string Topic, [property: JsonPropertyName("sentiment")] string Sentiment, [property: JsonPropertyName("urgency")] string Urgency, [property: JsonPropertyName("reason")] string Reason, [property: JsonPropertyName("provider")] string Provider, [property: JsonPropertyName("modelVersion")] string ModelVersion);
