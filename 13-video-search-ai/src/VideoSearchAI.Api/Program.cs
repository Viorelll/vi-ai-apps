using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Api.Middleware;
using VideoSearchAI.Database;
using VideoSearchAI.Infrastructure;
using VideoSearchAI.Infrastructure.Ai;
using VideoSearchAI.Infrastructure.Storage;
using VideoSearchAI.Services.Options;
using VideoSearchAI.Services.Services;
using VideoSearchAI.Services.Workers;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 52494336);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 52428800);
builder
    .Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
        o.InvalidModelStateResponseFactory =
            c => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = 400,
                    Title = "invalid_request",
                    Extensions =
                    {
                        { "code", "invalid_request" },
                        { "traceId", c.HttpContext.TraceIdentifier },
                    },
                }
            )
    );
builder.Services.AddProblemDetails();
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("App")
            ?? throw new InvalidOperationException("database_configuration_missing")
    )
);
builder
    .Services.AddOptions<PipelineOptions>()
    .Bind(builder.Configuration.GetSection("Pipeline"))
    .Validate(
        o => o.MaxRunTokens > 0 && o.MaxAttempts is > 0 and <= 5 && o.MaxDurationMs > 0,
        "invalid_pipeline_limits"
    )
    .ValidateOnStart();
builder
    .Services.AddHttpClient<AiClient>(h =>
    {
        h.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5081/");
        h.Timeout = TimeSpan.FromSeconds(60);
        h.DefaultRequestHeaders.Add(
            "X-Internal-Key",
            builder.Configuration["Ai:InternalKey"] ?? "dummy-internal-key-change-me"
        );
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<MediaProbe>();
builder.Services.AddScoped<UploadService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<SearchService>();
if (!migrate)
    builder.Services.AddHostedService<DurableWorker>();
var app = builder.Build();
if (migrate)
{
    await using var db = await app
        .Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
        .CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await app.Services.GetRequiredService<ObjectStore>().EnsureBucketAsync(CancellationToken.None);
    return;
}
app.UseMiddleware<ProblemMiddleware>();
app.MapGet("/health/live", () => new { status = "ok" });
app.MapGet(
    "/health/ready",
    async (IDbContextFactory<AppDbContext> factory, ObjectStore store, CancellationToken ct) =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(503);
        await store.EnsureBucketAsync(ct);
        return Results.Ok(new { status = "ok" });
    }
);
app.MapGet(
    "/api/config",
    () =>
        new
        {
            mode = builder.Configuration["Ai:Mode"] ?? "Demo",
            maxUploadBytes = 52428800,
            maxDurationMs = 1800000,
        }
);
app.MapControllers();
await app.RunAsync();

public partial class Program;
