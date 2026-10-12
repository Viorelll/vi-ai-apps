using System.Threading.RateLimiting;
using ImageAI.Api.Middleware;
using ImageAI.Database;
using ImageAI.Infrastructure;
using ImageAI.Infrastructure.Storage;
using ImageAI.Services.Documents;
using ImageAI.Services.Gateway;
using ImageAI.Services.Images;
using ImageAI.Services.Jobs;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 51L * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 51L * 1024 * 1024);
builder
    .Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
        o.InvalidModelStateResponseFactory = ValidationProblemFactory.Create
    );
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("App"))
);
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
builder.Services.AddHttpClient<DomainAiClient>(
    (sp, c) =>
    {
        c.BaseAddress = new(sp.GetRequiredService<IOptions<GatewayOptions>>().Value.BaseUrl);
        c.Timeout = TimeSpan.FromSeconds(150);
    }
);
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ImageProjectService>();
builder.Services.AddScoped<JobLeaser>();
builder.Services.AddScoped<GenerationProcessor>();
if (!migrate)
    builder.Services.AddHostedService<DurableGenerationWorker>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter(
            "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }
        )
    );
});
var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (
        db.Database.HasPendingModelChanges()
        || (await db.Database.GetPendingMigrationsAsync()).Any()
    )
        throw new InvalidOperationException(
            "Database migrations required; run API with --migrate first."
        );
}
await app.Services.GetRequiredService<ObjectStore>().InitializeAsync(CancellationToken.None);
app.UseMiddleware<ProblemMiddleware>();
app.Use(
    async (ctx, next) =>
    {
        ctx.Response.Headers.XContentTypeOptions = "nosniff";
        await next();
    }
);
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    async (AppDbContext db, ObjectStore store, CancellationToken ct) =>
    {
        await store.InitializeAsync(ct);
        return
            await db.Database.CanConnectAsync(ct)
            && !(await db.Database.GetPendingMigrationsAsync(ct)).Any()
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(503);
    }
);
app.MapGet(
    "/api/config",
    (IConfiguration config) =>
        Results.Ok(new { providerMode = config["ProviderMode"] ?? "Foundry", maxVariants = 12 })
);
await app.RunAsync();
