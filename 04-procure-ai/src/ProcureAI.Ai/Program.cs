using ProcureAI.Ai.Middleware;
using ProcureAI.Ai.Providers;
using ProcureAI.Ai.Tools;
using ProcureAI.Application.Exceptions;
using ProcureAI.Infrastructure;

if (args.Contains("--health-check"))
{
    Environment.ExitCode = await ProcureAI.Infrastructure.Health.HealthProbe.CheckAsync("http://localhost:8080/health/live");
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddSingleton<DemoAgentProvider>();
builder.Services.AddSingleton<ProcurementTools>();
builder.Services.AddHttpClient<FoundryAgentProvider>(client => { client.Timeout = TimeSpan.FromSeconds(60); client.MaxResponseContentBufferSize = 2 * 1024 * 1024; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<ExtractionProvider>(client => { client.Timeout = TimeSpan.FromSeconds(60); client.MaxResponseContentBufferSize = 2 * 1024 * 1024; })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
if (string.IsNullOrWhiteSpace(builder.Configuration["Internal:Key"])) throw new InvalidOperationException("Internal:Key required");
var mode = builder.Configuration["Ai:Mode"] ?? "Foundry";
if (mode is not ("Demo" or "Foundry")) throw new InvalidOperationException("Ai:Mode must be Demo or Foundry");
if ((builder.Configuration["Rag:Provider"] ?? "NoRag") != "NoRag")
    throw new InvalidOperationException("This build supports Rag:Provider=NoRag. Search, pgvector and File Search require a separate adapter.");
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (AppException exception)
    {
        await Results.Problem(statusCode: exception.Status, title: "AI operation failed", extensions: new Dictionary<string, object?>
        { ["code"] = exception.Code, ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
app.UseMiddleware<InternalKeyMiddleware>();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok", mode }));
app.MapGet("/health/ready", () => mode == "Demo" ||
    FoundryConfiguration.IsConfigured(app.Configuration)
    ? Results.Ok(new { status = "ready", mode }) : Results.Problem(statusCode: 503, title: "Foundry configuration required"));
app.MapControllers();
await app.RunAsync();
