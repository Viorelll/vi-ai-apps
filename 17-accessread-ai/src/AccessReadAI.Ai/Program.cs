using AccessReadAI.Ai.Middleware;
using AccessReadAI.Ai.Models;
using AccessReadAI.Ai.Providers;
using AccessReadAI.Contracts;
using AccessReadAI.Infrastructure;
using AccessReadAI.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddObjectStorage(builder.Configuration);
builder
    .Services.AddOptions<ProviderOptions>()
    .Bind(builder.Configuration.GetSection("Provider"))
    .Validate(x => x.Valid(), "Azure provider settings are missing or contain placeholders")
    .ValidateOnStart();
builder.Services.AddHttpClient<LayoutExtractor>(x => x.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<ResponsesClient>(x => x.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<TranslatorClient>(x => x.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<SpeechClient>(x => x.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<AccessibleTransformer>();
var app = builder.Build();
app.Use(
    async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception e)
        {
            context.Response.StatusCode =
                e is ApiFault f ? f.Status
                : e is ProviderFault { Transient: true } ? 503
                : 502;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    code = e is ApiFault or ProviderFault ? e.Message : "invalid_provider_response",
                    traceId = context.TraceIdentifier,
                }
            );
        }
    }
);
app.UseMiddleware<InternalKeyMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => new { status = "ok" });
app.MapGet(
    "/health/ready",
    async (IObjectStore store, CancellationToken ct) =>
    {
        await store.EnsureBucketAsync(ct);
        return Results.Ok(
            new
            {
                status = "ok",
                mode = app.Configuration.GetValue<bool>("Provider:Demo") ? "demo" : "azure",
            }
        );
    }
);
await app.RunAsync();
