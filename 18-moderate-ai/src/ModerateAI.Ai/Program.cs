using System.Security.Cryptography;
using System.Text;
using ModerateAI.Ai.Options;
using ModerateAI.Ai.Providers;
using ModerateAI.Ai.Services;
using ModerateAI.Domain.Models;
using ModerateAI.Infrastructure;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ModerateAI.Ai"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            t.AddOtlpExporter();
    });
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddStorage(builder.Configuration);
builder
    .Services.AddOptions<ProviderOptions>()
    .Bind(builder.Configuration.GetSection("Provider"))
    .Validate(
        ProviderOptions.Valid,
        "Provider must be Demo or configured HTTPS Foundry endpoints and keys."
    )
    .ValidateOnStart();
builder
    .Services.AddHttpClient<ContentSafetyAdapter>(c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder
    .Services.AddHttpClient<FoundryExplainer>(c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<AnalysisService>();
string key =
    builder.Configuration["Internal:Key"]
    ?? throw new InvalidOperationException("Internal key required");
if (key.Length < 24)
    throw new InvalidOperationException("Internal key must contain at least 24 characters.");
var app = builder.Build();
app.Use(
    async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(key)),
                actual = SHA256.HashData(
                    Encoding.UTF8.GetBytes(context.Request.Headers["X-Internal-Key"].ToString())
                );
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                context.Response.StatusCode = 401;
                return;
            }
        }
        try
        {
            await next(context);
        }
        catch (ProviderException e)
        {
            context.Response.Headers.RetryAfter = e.RetryAfterSeconds.ToString();
            await Results
                .Problem(
                    statusCode: e.Transient ? 503 : 422,
                    title: "AI provider request failed",
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", e.Code },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
        catch (DomainException e)
        {
            await Results
                .Problem(
                    statusCode: e.Status,
                    title: "Invalid request",
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", e.Code },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
    }
);
app.MapGet("/health/live", () => new { status = "ok" });
app.MapGet(
    "/health/ready",
    () => new { status = "ok", mode = app.Configuration["Provider:Mode"] ?? "Demo" }
);
app.MapControllers();
await app.RunAsync();

public partial class Program;
