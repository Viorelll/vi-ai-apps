using System.Threading.RateLimiting;
using AccessReadAI.Api.Middleware;
using AccessReadAI.Database;
using AccessReadAI.Infrastructure;
using AccessReadAI.Infrastructure.Storage;
using AccessReadAI.Services.Chat;
using AccessReadAI.Services.Clients;
using AccessReadAI.Services.Documents;
using AccessReadAI.Services.Jobs;
using AccessReadAI.Services.Readings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(x => x.Limits.MaxRequestBodySize = 55 * 1024 * 1024);
builder.Services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = 55 * 1024 * 1024);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(x =>
    x.UseNpgsql(builder.Configuration.GetConnectionString("App"))
);
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddHttpClient<DomainAiClient>(x =>
{
    x.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://ai:8080/");
    x.Timeout = TimeSpan.FromSeconds(180);
    x.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration["Internal:Key"]);
});
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ReadingService>();
builder.Services.AddScoped<JobStore>();
builder.Services.AddScoped<PipelineProcessor>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddHostedService<DurableWorker>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
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
    );
});
var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
await app.Services.GetRequiredService<IObjectStore>().EnsureBucketAsync(CancellationToken.None);
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health/live", () => new { status = "ok" });
app.MapGet(
    "/api/health",
    async (
        AppDbContext db,
        IObjectStore storage,
        IHttpClientFactory clients,
        CancellationToken ct
    ) =>
    {
        if (!await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(503);
        await storage.EnsureBucketAsync(ct);
        var client = clients.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        using var response = await client.GetAsync(
            new Uri(new Uri(app.Configuration["Ai:BaseUrl"]!), "health/ready"),
            ct
        );
        return response.IsSuccessStatusCode
            ? Results.Ok(new { status = "ok" })
            : Results.StatusCode(503);
    }
);
await app.RunAsync();
