using InspectAI.Infrastructure;
using InspectAI.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);
if ((builder.Configuration["Rag:Provider"] ?? "NoRag") != "NoRag")
    throw new InvalidOperationException(
        "This release supports NoRag. Alternative RAG providers require their own adapter and reindexing."
    );
builder.Services.AddControllers();
builder.Services.AddStorage(builder.Configuration).AddProviders();
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<InternalKeyMiddleware>();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    () =>
    {
        var foundryKey = builder.Configuration["Foundry:ApiKey"];
        var extractionKey = builder.Configuration["ContentUnderstanding:ApiKey"];
        bool configured(string? key) =>
            !string.IsNullOrWhiteSpace(key)
            && !key.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);
        var foundryConfigured = configured(foundryKey);
        var extractorConfigured = configured(extractionKey);
        return Results.Json(
            new
            {
                status = foundryConfigured && extractorConfigured ? "ok" : "configuration_required",
                foundryConfigured,
                extractorConfigured,
            },
            statusCode: foundryConfigured && extractorConfigured ? 200 : 503
        );
    }
);
app.MapControllers();
await app.RunAsync();
