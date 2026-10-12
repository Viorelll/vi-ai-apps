using Microsoft.EntityFrameworkCore;
using ProcureAI.Api.Middleware;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Services;
using ProcureAI.Database.Persistence;
using ProcureAI.Infrastructure;
using ProcureAI.Infrastructure.Ai;

if (args.Contains("--health-check"))
{
    Environment.ExitCode = await ProcureAI.Infrastructure.Health.HealthProbe.CheckAsync("http://localhost:8080/health/live");
    return;
}

var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 55L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 55L * 1024 * 1024);
builder.Services.AddControllers();
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("App")
    ?? throw new InvalidOperationException("ConnectionStrings:App required")));
builder.Services.AddScoped<IProcurementRepository, ProcurementRepository>();
builder.Services.AddScoped<IJobStore, JobStore>();
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddHttpClient<IAiGateway, HttpAiGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5081/");
    client.Timeout = TimeSpan.FromSeconds(115);
});
builder.Services.AddHttpClient("health-ai", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:5081/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<OfferScorer>();
builder.Services.AddSingleton<EvidenceValidator>();
builder.Services.AddScoped<RunService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<WorkflowProcessor>();
builder.Services.AddScoped<DocumentProcessor>();
if (!migrate) builder.Services.AddHostedService<DurableWorker>();
var app = builder.Build();
if (migrate)
{
    await using var db = await app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
    return;
}
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
await app.RunAsync();
