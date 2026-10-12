using Amazon.S3;
using DataAI.Api.Services;
using DataAI.Application.Abstractions;
using DataAI.Database.Persistence;
using DataAI.Domain.Entities;
using DataAI.Infrastructure;
using Microsoft.EntityFrameworkCore;

var migrateOnly = args.Contains("--migrate");
var webArgs = args.Where(x => x != "--migrate").ToArray();
var builder = WebApplication.CreateBuilder(webArgs);
builder.Services.AddControllers();
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("App")));
builder.Services.AddDataAIInfrastructure(builder.Configuration);
builder.Services.AddScoped<IDataAIService, DataAIService>();
var app = builder.Build();

if (migrateOnly)
{
    await using var scope = app.Services.CreateAsyncScope();
    await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
    await db.Database.MigrateAsync();
    return;
}

app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500, traceId = context.TraceIdentifier });
}));
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (IDbContextFactory<AppDbContext> factory, CancellationToken ct) =>
{
    try { await using var db = await factory.CreateDbContextAsync(ct); await db.Database.CanConnectAsync(ct); return Results.Ok(new { status = "ok" }); }
    catch { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
});
app.MapControllers();
await using (var scope = app.Services.CreateAsyncScope())
{
    var objects = scope.ServiceProvider.GetRequiredService<IObjectStore>();
    await objects.EnsureBucketAsync(CancellationToken.None);
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    if (!await db.Datasets.AnyAsync())
    {
        var dataset = new Dataset
        {
            Name = "Sample sales · EUR",
            Revision = 1,
            Rows = [
            new() { SoldOn = new DateOnly(2026, 1, 5), Category = "Hardware", Quantity = 2, UnitPrice = 10m, Currency = "EUR" },
            new() { SoldOn = new DateOnly(2026, 1, 6), Category = "Hardware", Quantity = 1, UnitPrice = 5m, Currency = "EUR" },
            new() { SoldOn = new DateOnly(2026, 1, 7), Category = "Software", Quantity = 3, UnitPrice = 7m, Currency = "EUR" }
        ]
        };
        db.Datasets.Add(dataset);
        await db.SaveChangesAsync();
    }
}
await app.RunAsync();

public partial class Program;
