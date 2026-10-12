using VideoStudioAI.Ai.Interfaces;
using VideoStudioAI.Ai.Middleware;
using VideoStudioAI.Ai.Models;
using VideoStudioAI.Ai.Providers;
using VideoStudioAI.Ai.Services;

var builder = WebApplication.CreateBuilder(args);
builder
    .Services.AddOptions<FoundryOptions>()
    .Bind(builder.Configuration.GetSection("Foundry"))
    .Validate(o => o.Mode is "Foundry" or "Demo", "Mode must be Foundry or Demo")
    .Validate(
        o => o.Seconds.Length > 0 && o.Sizes.Length > 0,
        "Configure supported durations and sizes"
    )
    .Validate(
        o =>
            o.Mode == "Demo"
            || (
                Uri.TryCreate(o.OpenAiEndpoint, UriKind.Absolute, out var u)
                && u.Scheme == "https"
                && u.AbsolutePath.EndsWith("/openai/v1/", StringComparison.Ordinal)
                && !o.ApiKey.StartsWith("dummy", StringComparison.Ordinal)
                && !o.ChatDeployment.StartsWith("dummy", StringComparison.Ordinal)
                && !o.VideoDeployment.StartsWith("dummy", StringComparison.Ordinal)
            ),
        "Foundry mode requires a real HTTPS v1 endpoint, key and deployments. Use Demo explicitly for offline fixtures."
    )
    .ValidateOnStart();
if (string.IsNullOrWhiteSpace(builder.Configuration["InternalKey"]))
    throw new InvalidOperationException("InternalKey is required.");
builder.Services.AddHttpClient<FoundryProvider>(http => http.Timeout = TimeSpan.FromSeconds(40));
if (builder.Configuration["Foundry:Mode"] == "Demo")
    builder.Services.AddSingleton<IProvider, DemoProvider>();
else
    builder.Services.AddTransient<IProvider>(sp => sp.GetRequiredService<FoundryProvider>());
builder.Services.AddScoped<ExtractionService>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseMiddleware<GatewayMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.Run();
