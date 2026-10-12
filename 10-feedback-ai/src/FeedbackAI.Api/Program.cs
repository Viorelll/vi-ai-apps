using Amazon.S3;
using FeedbackAI.Api.Services;
using FeedbackAI.Database.Persistence;
using FeedbackAI.Application.Abstractions;
using FeedbackAI.Infrastructure.Storage;
using FeedbackAI.Api.Middleware;
using Microsoft.EntityFrameworkCore;

var migrateOnly = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--migrate").ToArray());
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddProblemDetails();
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("App"), n => n.MigrationsAssembly("FeedbackAI.Api")));
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials(builder.Configuration["Storage:AccessKey"], builder.Configuration["Storage:SecretKey"]), new AmazonS3Config { ServiceURL = builder.Configuration["Storage:Endpoint"], ForcePathStyle = true, AuthenticationRegion = "us-east-1" }));
builder.Services.AddSingleton(sp => new S3ObjectStore(sp.GetRequiredService<IAmazonS3>(), builder.Configuration["Storage:Bucket"] ?? "feedbackai"));
builder.Services.AddHttpClient<DomainAiClient>(client => { client.BaseAddress = new Uri(builder.Configuration["Ai:BaseUrl"] ?? "http://localhost:8081/"); client.Timeout = TimeSpan.FromSeconds(100); });
builder.Services.AddScoped<IClassifier>(sp => sp.GetRequiredService<DomainAiClient>());
builder.Services.AddScoped<FeedbackService>();
var app = builder.Build();
if (migrateOnly)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
    return;
}
app.UseMiddleware<ApiExceptionMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (IDbContextFactory<AppDbContext> factory, CancellationToken ct) => { await using var db = await factory.CreateDbContextAsync(ct); return await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503); });
app.Run();
public partial class Program;
