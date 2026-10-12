using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Api.Middleware;
using VideoStudioAI.Database;
using VideoStudioAI.Infrastructure;
using VideoStudioAI.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var migrate = args.Contains("--migrate");
builder.Services.AddVideoStudio(builder.Configuration, !migrate);
builder
    .Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory =
            context => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(
                new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = 400,
                    Title = "Invalid request body.",
                    Extensions =
                    {
                        ["code"] = "invalid_request",
                        ["traceId"] = context.HttpContext.TraceIdentifier,
                    },
                }
            );
    });
builder.Services.AddProblemDetails();
var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}
app.UseMiddleware<ProblemMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet(
    "/health/ready",
    async (AppDbContext db, IObjectStore storage, CancellationToken ct) =>
    {
        if (!await db.Database.CanConnectAsync(ct))
            return Results.StatusCode(503);
        if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            return Results.StatusCode(503);
        await storage.EnsureBucketAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
);
app.Run();

public partial class Program;
