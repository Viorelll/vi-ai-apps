using DocuAI.Application;
using DocuAI.Database.Persistence;
using DocuAI.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 55 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = 55 * 1024 * 1024);
builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("App")
    ?? throw new InvalidOperationException("ConnectionStrings:App is required.");

builder.Services
    .AddDocuAiDatabase(connectionString)
    .AddDocuAiInfrastructure(builder.Configuration)
    .AddDocuAiApplication();

var app = builder.Build();

if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DocuAiDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    return;
}

app.MapControllers();
await app.RunAsync();
