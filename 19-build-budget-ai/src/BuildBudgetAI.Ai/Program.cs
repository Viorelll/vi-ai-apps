using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using BuildBudgetAI.Ai.Models;
using BuildBudgetAI.Ai.Providers;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
var foundry = builder.Configuration.GetSection("Foundry").Get<FoundryOptions>() ?? new();
var extraction =
    builder.Configuration.GetSection("ContentUnderstanding").Get<ExtractionOptions>() ?? new();
if (
    foundry.Mode is not ("Demo" or "Foundry")
    || foundry.MaxToolRounds is < 0 or > 5
    || foundry.MaxRunTokens is < 1200 or > 15000
)
    throw new InvalidOperationException("invalid_ai_configuration");
if (
    foundry.Mode == "Foundry"
    && (
        foundry.OpenAiEndpoint.Contains("RESOURCE")
        || foundry.ApiKey.StartsWith("dummy")
        || extraction.Endpoint.Contains("RESOURCE")
        || extraction.ApiKey.StartsWith("dummy")
    )
)
    throw new InvalidOperationException(
        "Configure real Foundry and Content Understanding credentials before enabling Foundry mode."
    );
builder
    .Services.AddHttpClient<OfferExtractionProvider>(h => h.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton(foundry);
builder.Services.AddSingleton(extraction);
builder
    .Services.AddHttpClient<FoundryProvider>(h => h.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder
    .Services.AddHttpClient<ExtractionProvider>(h => h.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new();
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton<IAmazonS3>(
    new AmazonS3Client(
        new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
        new AmazonS3Config
        {
            ServiceURL = storage.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
        }
    )
);
builder.Services.AddSingleton<IObjectStore, S3ObjectStore>();
var app = builder.Build();
byte[] key = SHA256.HashData(
    Encoding.UTF8.GetBytes(builder.Configuration["Internal:Key"] ?? "dummy-internal-key-change-me")
);
app.Use(
    async (ctx, next) =>
    {
        if (
            ctx.Request.Path != "/health/live"
            && !CryptographicOperations.FixedTimeEquals(
                key,
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Internal-Key"].ToString())
                )
            )
        )
        {
            ctx.Response.StatusCode = 401;
            return;
        }
        try
        {
            await next(ctx);
        }
        catch (DomainException e)
        {
            await Results
                .Problem(
                    statusCode: e.Status,
                    title: e.Code,
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", e.Code },
                        { "traceId", ctx.TraceIdentifier },
                    }
                )
                .ExecuteAsync(ctx);
        }
    }
);
app.UseExceptionHandler();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    () =>
        Results.Ok(
            new
            {
                status = "ready",
                mode = foundry.Mode,
                rag = "NoRag",
            }
        )
);
await app.RunAsync();
