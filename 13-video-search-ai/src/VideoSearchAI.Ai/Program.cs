using VideoSearchAI.Ai.Middleware;
using VideoSearchAI.Ai.Options;
using VideoSearchAI.Ai.Providers;
using VideoSearchAI.Ai.Providers.Extraction;
using VideoSearchAI.Ai.Providers.Foundry;
using VideoSearchAI.Ai.Providers.Search;
using VideoSearchAI.Ai.Services;
using VideoSearchAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddStorage(builder.Configuration);
builder
    .Services.AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection("Ai"))
    .Validate(
        o =>
            o.Mode is "Demo" or "Foundry"
            && o.InternalKey.Length >= 16
            && o.EmbeddingDimensions > 0
            && o.MaxToolRounds is > 0 and <= 5,
        "invalid_ai_configuration"
    )
    .Validate(
        o =>
            o.Mode == "Demo"
            || new[] { o.OpenAiEndpoint, o.ContentEndpoint, o.SearchEndpoint }.All(s =>
                Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == "https"
            ),
        "https_provider_endpoints_required"
    )
    .ValidateOnStart();
builder.Services.AddSingleton<VideoUnderstandingMapper>();
builder.Services.AddSingleton<DemoExtractor>();
builder.Services.AddSingleton<DemoRetrievalProvider>();
builder.Services.AddScoped<AnswerGenerator>();
builder.Services.AddScoped<AiGatewayService>();
builder
    .Services.AddHttpClient<FoundryV1Client>(h => h.Timeout = TimeSpan.FromSeconds(55))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder
    .Services.AddHttpClient<ContentUnderstandingClient>(h => h.Timeout = TimeSpan.FromSeconds(55))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder
    .Services.AddHttpClient<AzureSearchProvider>(h => h.Timeout = TimeSpan.FromSeconds(55))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var app = builder.Build();
app.Use(
    async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            var code = e is ProviderException p ? p.Code : "invalid_provider_response";
            var status = e is ProviderException pe ? pe.Status : 502;
            if (e is ProviderException { RetryAfter: not null } retry)
                context.Response.Headers.RetryAfter = (
                    (int)retry.RetryAfter.Value.TotalSeconds
                ).ToString();
            await Results
                .Problem(
                    statusCode: status,
                    title: code,
                    extensions: new Dictionary<string, object?>
                    {
                        { "code", code },
                        { "traceId", context.TraceIdentifier },
                    }
                )
                .ExecuteAsync(context);
        }
    }
);
app.UseMiddleware<InternalAuthenticationMiddleware>();
app.MapGet("/health/live", () => new { status = "ok" });
app.MapGet(
    "/health/ready",
    (Microsoft.Extensions.Options.IOptions<AiOptions> o) =>
        new { status = "ok", mode = o.Value.Mode }
);
app.MapControllers();
await app.RunAsync();

public partial class Program;
