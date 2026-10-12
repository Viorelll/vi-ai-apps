using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.EntityFrameworkCore;
using SupportAI.Api.Middleware;
using SupportAI.Application.Abstractions;
using SupportAI.Application.Services;
using SupportAI.Database.Persistence;
using SupportAI.Infrastructure;
using SupportAI.Infrastructure.Ai;
var migrate = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(x => x.Limits.MaxRequestBodySize = 55L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(x => x.MultipartBodyLengthLimit = 55L * 1024 * 1024);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDbContextFactory<AppDbContext>(x => x.UseNpgsql(builder.Configuration.GetConnectionString("App") ?? throw new InvalidOperationException("ConnectionStrings:App required")));
builder.Services.AddSingleton<IOrderReader, SeedOrderReader>();
builder.Services.AddStorage(builder.Configuration);
builder.Services.AddHttpClient<IAiGateway, HttpAiGateway>(x => { x.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:8081/"); x.Timeout = TimeSpan.FromSeconds(100); });
builder.Services.AddHttpClient("health", x => x.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<SupportService>();
builder.Services.AddScoped<ActionService>();
builder.Services.AddScoped<PolicySeeder>();
if (!migrate) builder.Services.AddHostedService<DurableWorker>();
builder.Services.AddOpenTelemetry().ConfigureResource(x => x.AddService("SupportAI.Api")).WithTracing(x =>
{
    x.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
    if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        x.AddOtlpExporter();
});
if ((builder.Configuration["Rag:Provider"] ?? "NoRag") != "NoRag") throw new InvalidOperationException("This profile supports only Rag:Provider=NoRag.");
var app = builder.Build();
if (migrate) { await using var scope = app.Services.CreateAsyncScope(); await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync(); await db.Database.MigrateAsync(); return; }
app.UseMiddleware<ProblemMiddleware>();
app.MapControllers();
if (app.Configuration.GetValue("Seed:Enabled", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<PolicySeeder>().SeedAsync(CancellationToken.None);
}
app.Run();
public partial class Program;
