using System.Threading.RateLimiting;
using ContractAI.Database.Jobs;
using ContractAI.Database.Persistence;
using ContractAI.Infrastructure;
using ContractAI.Infrastructure.Http;
using ContractAI.Services.Contracts;
using ContractAI.Services.Documents;
using ContractAI.Services.Jobs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 55 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 55 * 1024 * 1024
);
builder.Services.AddControllers();
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseNpgsql(
        builder.Configuration.GetConnectionString("App")
            ?? throw new InvalidOperationException("ConnectionStrings:App is required.")
    )
);
builder.Services.AddStorage(builder.Configuration).AddGatewayClient(builder.Configuration);
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddSingleton<JobStore>();
if (!migrate)
    builder.Services.AddHostedService<DurableWorker>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddConcurrencyLimiter(
        "ai",
        l =>
        {
            l.PermitLimit = 2;
            l.QueueLimit = 4;
            l.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }
    );
});
var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    return;
}
app.UseMiddleware<ExceptionMiddleware>();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    async (
        IDbContextFactory<AppDbContext> factory,
        ContractAI.Infrastructure.Abstractions.IObjectStore storage,
        IHttpClientFactory httpFactory,
        CancellationToken ct
    ) =>
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.Database.CanConnectAsync(ct))
                return Results.StatusCode(503);
            await storage.EnsureBucketAsync(ct);
            using var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            using var response = await client.GetAsync(
                (builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5101/") + "health/ready",
                ct
            );
            return response.IsSuccessStatusCode
                ? Results.Ok(new { status = "ok" })
                : Results.StatusCode(503);
        }
        catch
        {
            return Results.StatusCode(503);
        }
    }
);
app.MapControllers();
await app.RunAsync();
