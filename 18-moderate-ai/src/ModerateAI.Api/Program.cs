using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModerateAI.Database;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Models;
using ModerateAI.Infrastructure;
using ModerateAI.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

bool migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
string connection =
    builder.Configuration.GetConnectionString("App")
    ?? throw new InvalidOperationException("ConnectionStrings:App required");
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connection));
builder
    .Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ModerateAI.Api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            t.AddOtlpExporter();
    });
builder
    .Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
        o.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
            new ProblemDetails
            {
                Status = 400,
                Title = "Invalid request",
                Extensions =
                {
                    ["code"] = "validation_failed",
                    ["traceId"] = context.HttpContext.TraceIdentifier,
                },
            }
        )
    );
builder.Services.AddProblemDetails();
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddScoped<ModerationService>();
builder.Services.AddScoped<DocumentService>();
builder
    .Services.AddHttpClient<IAnalysisGateway, AnalysisGateway>(c =>
    {
        c.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://ai:8080/");
        c.Timeout = TimeSpan.FromSeconds(100);
        c.DefaultRequestHeaders.Add(
            "X-Internal-Key",
            builder.Configuration["Internal:Key"]
                ?? throw new InvalidOperationException("Internal key required")
        );
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient(
    "readiness",
    c =>
    {
        c.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://ai:8080/");
        c.Timeout = TimeSpan.FromSeconds(3);
    }
);
if (!migrate)
    builder.Services.AddHostedService<DurableWorker>();
var app = builder.Build();
if (migrate)
{
    await using var db = await app
        .Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
        .CreateDbContextAsync();
    await db.Database.MigrateAsync();
    return;
}
await app.Services.GetRequiredService<IObjectStore>().EnsureBucketAsync(CancellationToken.None);
app.Use(
    async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers.CacheControl = "no-store";
        try
        {
            await next(context);
        }
        catch (DomainException e)
        {
            await Results
                .Problem(
                    statusCode: e.Status,
                    title: "Request could not be completed",
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", e.Code },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception e)
        {
            app.Logger.LogError(
                "Request {TraceId} failed: {Type}",
                context.TraceIdentifier,
                e.GetType().Name
            );
            await Results
                .Problem(
                    statusCode: 503,
                    title: "Service temporarily unavailable",
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", "service_unavailable" },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
    }
);
app.MapGet("/health/live", () => new { status = "ok" });
app.MapGet(
    "/api/health",
    async (
        IDbContextFactory<AppDbContext> factory,
        IObjectStore store,
        IHttpClientFactory http,
        CancellationToken ct
    ) =>
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            bool database = await db.Database.CanConnectAsync(ct),
                storage = await store.ReadyAsync(ct);
            using var response = await http.CreateClient("readiness").GetAsync("health/ready", ct);
            if (!database || !storage || !response.IsSuccessStatusCode)
                return Results.StatusCode(503);
            var gateway = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
                ct
            );
            return Results.Ok(
                new
                {
                    status = "ok",
                    mode = gateway.GetProperty("mode").GetString(),
                    policyVersion = ModerateAI.Domain.Services.PolicyEvaluator.Version,
                }
            );
        }
        catch
        {
            return Results.StatusCode(503);
        }
    }
);
app.MapControllers();
await app.RunAsync();

public partial class Program;
