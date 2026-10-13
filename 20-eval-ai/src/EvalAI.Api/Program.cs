using EvalAI.Database.Jobs;
using EvalAI.Database.Persistence;
using EvalAI.Infrastructure;
using EvalAI.Infrastructure.Http;
using EvalAI.Infrastructure.Abstractions;
using EvalAI.Services.Documents;
using EvalAI.Services.Evaluations;
using EvalAI.Services.Jobs;
using Microsoft.EntityFrameworkCore;
if (args.Contains("--health"))
{
    try { using var health = new HttpClient { Timeout = TimeSpan.FromSeconds(2) }; using var response = await health.GetAsync("http://localhost:8080/health/ready"); Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1; } catch { Environment.ExitCode = 1; }
    return;
}
var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 55 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 55 * 1024 * 1024);
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = c => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { code = "invalid_request", traceId = c.HttpContext.TraceIdentifier, detail = "Request fields are missing or invalid." }));
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("App") ?? throw new InvalidOperationException("ConnectionStrings:App required.")));
builder.Services.AddStorage(builder.Configuration).AddGatewayClient(builder.Configuration);
builder.Services.AddSingleton<VariantCatalog>(); builder.Services.AddScoped<DatasetService>(); builder.Services.AddScoped<EvaluationService>(); builder.Services.AddScoped<DocumentService>(); builder.Services.AddScoped<ChatService>(); builder.Services.AddSingleton<JobStore>();
if (!migrate) builder.Services.AddHostedService<DurableWorker>();
var app = builder.Build();
if (migrate) { await using var scope = app.Services.CreateAsyncScope(); var f = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>(); await using var db = await f.CreateDbContextAsync(); await db.Database.MigrateAsync(); await scope.ServiceProvider.GetRequiredService<DatasetService>().SeedAsync(default); return; }
app.UseMiddleware<ExceptionMiddleware>();
app.Use(async (c, next) => { c.Response.Headers["X-Content-Type-Options"] = "nosniff"; await next(); });
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (IDbContextFactory<AppDbContext> f, IObjectStore storage, IHttpClientFactory hf, CancellationToken ct) =>
{
    try { await using var db = await f.CreateDbContextAsync(ct); if (!await db.Database.CanConnectAsync(ct)) return Results.StatusCode(503); await storage.EnsureBucketAsync(ct); using var client = hf.CreateClient(); client.Timeout = TimeSpan.FromSeconds(3); using var response = await client.GetAsync((builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5101/") + "health/ready", ct); return response.IsSuccessStatusCode ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503); } catch { return Results.StatusCode(503); }
});
app.MapControllers(); await app.RunAsync();
public partial class Program;
