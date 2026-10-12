using ImageAI.Ai.Options;
using ImageAI.Ai.Providers;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 142L * 1024 * 1024);
builder.Services.AddHttpClient<FoundryPromptService>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddControllers();
builder.Services.Configure<ImagesOptions>(builder.Configuration.GetSection("Images"));
var mode = builder.Configuration["Images:Mode"] ?? "Foundry";
if (mode == "Demo")
    builder.Services.AddSingleton<IImageProvider, DemoImageProvider>();
else if (mode == "Foundry")
    builder.Services.AddHttpClient<IImageProvider, FoundryImageProvider>(c =>
        c.Timeout = TimeSpan.FromSeconds(120)
    );
else
    throw new InvalidOperationException("Images:Mode must be Foundry or Demo");
var endpoint = builder.Configuration["Images:Endpoint"] ?? "";
if (
    mode == "Foundry"
    && (
        !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        || uri.Scheme != "https"
        || uri.AbsolutePath != "/"
        || !string.IsNullOrEmpty(uri.UserInfo)
    )
)
    throw new InvalidOperationException("Images:Endpoint must be an HTTPS resource root");
var app = builder.Build();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet(
    "/health/ready",
    () =>
    {
        var configured =
            mode == "Demo"
            || !(
                endpoint.Contains("RESOURCE", StringComparison.OrdinalIgnoreCase)
                || (builder.Configuration["Images:ApiKey"] ?? "replace-").StartsWith("replace-")
                || (builder.Configuration["Images:Deployment"] ?? "replace-").StartsWith("replace-")
                || (builder.Configuration["Images:ChatDeployment"] ?? "").StartsWith("replace-")
            );
        return configured
            ? Results.Ok(
                new
                {
                    status = "ready",
                    mode,
                    azureVerified = false,
                }
            )
            : Results.Json(new { code = "provider_not_configured", mode }, statusCode: 503);
    }
);
await app.RunAsync();
