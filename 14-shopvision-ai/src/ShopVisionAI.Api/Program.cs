using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Api.Middleware;
using ShopVisionAI.Database;
using ShopVisionAI.Infrastructure.Storage;
using ShopVisionAI.Services.Clients;
using ShopVisionAI.Services.Documents;
using ShopVisionAI.Services.Matching;
using ShopVisionAI.Services.Searches;
using ShopVisionAI.Services.Workers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("App"))
);
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<VisualSearchService>();
builder.Services.AddSingleton<MatchingService>();
builder.Services.AddHttpClient<DomainAiClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(115);
    c.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration["Internal:Key"]);
});
if (!args.Contains("--migrate"))
    builder.Services.AddHostedService<DurableVisionWorker>();
var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
await app.Services.GetRequiredService<ObjectStore>().EnsureBucketAsync(CancellationToken.None);
app.UseExceptionHandler();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet(
    "/api/health/ready",
    async (AppDbContext db, ObjectStore store, CancellationToken ct) =>
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        await store.EnsureBucketAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
);
app.Run();

public partial class ApiProgram;
