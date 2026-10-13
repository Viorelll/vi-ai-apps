using EvalAI.Infrastructure;
using EvalAI.Infrastructure.Http;
if (args.Contains("--health"))
{
    try { using var health = new HttpClient { Timeout = TimeSpan.FromSeconds(2) }; using var response = await health.GetAsync("http://localhost:8080/health/ready"); Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1; } catch { Environment.ExitCode = 1; }
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(); builder.Services.AddStorage(builder.Configuration).AddProviders();
var mode = builder.Configuration["Foundry:Mode"] ?? "Live";
if (mode is not ("Demo" or "Live")) throw new InvalidOperationException("Foundry:Mode must be Demo or Live.");
if ((builder.Configuration["Rag:Provider"] ?? "NoRag") != "NoRag") throw new InvalidOperationException("This build supports NoRag only; configure explicit retrieval adapters before enabling another provider.");
var app = builder.Build(); app.UseMiddleware<ExceptionMiddleware>(); app.UseMiddleware<InternalKeyMiddleware>();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () =>
{
    var configured = mode == "Demo" || (!string.IsNullOrWhiteSpace(builder.Configuration["Foundry:ApiKey"]) && !(builder.Configuration["Foundry:ApiKey"] ?? "").StartsWith("replace-") && Uri.TryCreate(builder.Configuration["Foundry:OpenAiEndpoint"], UriKind.Absolute, out var u) && u.Scheme == "https");
    return configured ? Results.Ok(new { status = "ok", mode, rag = "NoRag", liveConnectivityVerified = false }) : Results.StatusCode(503);
});
app.MapControllers(); await app.RunAsync();
