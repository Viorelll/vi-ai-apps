using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SupportAI.Ai.Middleware;
using SupportAI.Ai.Models;
using SupportAI.Ai.Services;
using SupportAI.Domain.Models;
using SupportAI.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddOptions<FoundryOptions>().Bind(builder.Configuration.GetSection("Foundry")).Validate(x => x.Mode is "Foundry" or "Demo" && x.MaxToolRounds is > 0 and <= 5 && x.MaxRunTokens > 0 && x.MaxOutputTokens is > 0 and <= 4000, "Invalid Foundry limits").ValidateOnStart();
builder.Services.AddHttpClient<FoundryClient>(x => x.Timeout = TimeSpan.FromSeconds(60)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<ContentUnderstandingService>(x => x.Timeout = TimeSpan.FromSeconds(60)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<PromptShieldService>(x => x.Timeout = TimeSpan.FromSeconds(15)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<SupportToolExecutor>();
builder.Services.AddScoped<FoundrySupportService>();
builder.Services.AddOpenTelemetry().ConfigureResource(x => x.AddService("SupportAI.Ai")).WithTracing(x =>
{
    x.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        x.AddOtlpExporter();
});
if ((builder.Configuration["Rag:Provider"] ?? "NoRag") != "NoRag") throw new InvalidOperationException("This profile supports only Rag:Provider=NoRag.");
var app = builder.Build();
app.Use(async (context, next) => { try { await next(); } catch (SupportException e) { await Results.Problem(statusCode: e.Status, title: e.Code, extensions: new Dictionary<string, object?> { { "code", e.Code }, { "traceId", context.TraceIdentifier }, { "retryAfterSeconds", e.RetryAfterSeconds } }).ExecuteAsync(context); } catch (Exception e) when (e is System.Text.Json.JsonException or HttpRequestException) { await Results.Problem(statusCode: 502, title: "provider_contract_error", extensions: new Dictionary<string, object?> { { "code", "provider_contract_error" }, { "traceId", context.TraceIdentifier } }).ExecuteAsync(context); } });
app.UseMiddleware<InternalKeyMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () =>
{
    var mode = app.Configuration["Foundry:Mode"] ?? "Foundry";
    var key = app.Configuration["Foundry:ApiKey"];
    if (mode == "Foundry" && (string.IsNullOrWhiteSpace(key) || key.StartsWith("replace-") || !Uri.TryCreate(app.Configuration["Foundry:OpenAiEndpoint"], UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"))
        return Results.Problem(statusCode: 503, title: "foundry_not_configured");
    return Results.Ok(new
    {
        status = "ready",
        mode
    });
});
app.Run();
public partial class Program;
