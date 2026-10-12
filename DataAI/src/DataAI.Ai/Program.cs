using DataAI.Domain.Contracts.AiGateway;
using DataAI.Ai.Providers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<FoundryPlanner>(client => client.Timeout = TimeSpan.FromSeconds(90));
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", (FoundryPlanner planner) => Results.Ok(new { status = "ok", foundryConfigured = planner.IsConfigured }));
app.MapPost("/plan", async (PlanRequest request, FoundryPlanner planner, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000 || request.Categories.Count == 0) return Results.BadRequest(new { code = "invalid_plan_request" });
    try { return Results.Ok(await planner.PlanAsync(request, ct)); }
    catch (HttpRequestException) { return Results.Problem("The AI provider request failed.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "foundry_request_failed" }); }
    catch (InvalidOperationException) { return Results.Problem("The AI provider returned an invalid plan.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "foundry_invalid_response" }); }
});
app.MapPost("/explain", async (ExplainRequest request, FoundryPlanner planner, CancellationToken ct) =>
{
    try { return Results.Ok(await planner.ExplainAsync(request, ct)); }
    catch (HttpRequestException) { return Results.Problem("The AI provider request failed.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "foundry_request_failed" }); }
});
await app.RunAsync();
