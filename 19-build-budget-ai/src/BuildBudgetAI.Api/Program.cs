using System.Threading.RateLimiting;
using Amazon.Runtime;
using Amazon.S3;
using BuildBudgetAI.Api.Services;
using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Infrastructure;
using BuildBudgetAI.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 53 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 53 * 1024 * 1024);
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("App"))
);
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ChatService>();
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new();
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton<IAmazonS3>(
    new AmazonS3Client(
        new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = storage.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
        }
    )
);
builder.Services.AddSingleton<IObjectStore, S3ObjectStore>();
builder.Services.AddHttpClient<IAiClient, AiHttpClient>(h =>
{
    h.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5081/");
    h.Timeout = TimeSpan.FromSeconds(100);
    h.DefaultRequestHeaders.Add(
        "X-Internal-Key",
        builder.Configuration["Internal:Key"] ?? "dummy-internal-key-change-me"
    );
});
builder.Services.AddHostedService<DurableWorker>();
builder.Services.AddRateLimiter(o =>
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetFixedWindowLimiter(
            "local",
            _ =>
                new()
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
        )
    )
);
var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
app.Use(
    async (ctx, next) =>
    {
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            await next(ctx);
        }
        catch (Exception e)
            when (e is DomainException or DbUpdateConcurrencyException or OverflowException)
        {
            int status =
                e is DomainException d ? d.Status
                : e is DbUpdateConcurrencyException ? 409
                : 400;
            string code =
                e is DomainException de ? de.Code
                : e is DbUpdateConcurrencyException ? "revision_conflict"
                : "numeric_overflow";
            await Results
                .Problem(
                    statusCode: status,
                    title: code,
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", code },
                        { "traceId", ctx.TraceIdentifier },
                    }
                )
                .ExecuteAsync(ctx);
        }
    }
);
app.UseExceptionHandler();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/api/health",
    () => Results.Ok(new { status = "ok", mode = builder.Configuration["Ai:Mode"] ?? "Demo" })
);
app.MapGet(
    "/health/ready",
    async (AppDbContext db, IObjectStore store, CancellationToken ct) =>
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        await store.InitializeAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
);
await app.Services.GetRequiredService<IObjectStore>().InitializeAsync(CancellationToken.None);
await app.RunAsync();
