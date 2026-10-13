using Microsoft.EntityFrameworkCore;
using TutorAI.Database.Jobs;
using TutorAI.Database.Persistence;
using TutorAI.Infrastructure;
using TutorAI.Infrastructure.Http;
using TutorAI.Services.Documents;
using TutorAI.Services.Jobs;
using TutorAI.Services.Lessons;
using TutorAI.Services.Tutoring;

var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 55L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 55L * 1024 * 1024
);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDbContextFactory<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("App"))
);
builder.Services.AddStorage(builder.Configuration).AddGatewayClient(builder.Configuration);
builder.Services.AddScoped<TutorService>();
builder.Services.AddScoped<LessonService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddSingleton<JobStore>();
if (!builder.Configuration.GetValue<bool>("Worker:Disabled"))
    builder.Services.AddHostedService<DurableWorker>();
var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = await scope
        .ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>()
        .CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(db);
    return;
}
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    async (IDbContextFactory<AppDbContext> f, CancellationToken ct) =>
    {
        await using var db = await f.CreateDbContextAsync(ct);
        return await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(503);
    }
);
app.MapGet(
    "/api/status",
    () => new { demoMode = app.Configuration.GetValue<bool>("Ai:DemoMode"), domain = "TutorAI" }
);
await app.RunAsync();

public partial class Program { }
