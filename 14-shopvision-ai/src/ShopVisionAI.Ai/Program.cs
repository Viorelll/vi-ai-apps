using ShopVisionAI.Ai.Providers;
using ShopVisionAI.Infrastructure.Security;
using ShopVisionAI.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ShopVisionAI.Ai.Tools.CatalogSnapshotTool>();
builder.Services.AddHttpClient<FoundryGroundedProvider>(c => c.Timeout = TimeSpan.FromSeconds(100));
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ShopVisionAI.Ai.Middleware.AiExceptionHandler>();
builder.Services.AddObjectStorage(builder.Configuration);
builder
    .Services.AddOptions<FoundryOptions>()
    .BindConfiguration("Foundry")
    .Validate(o => o.Mode is "Demo" or "Foundry", "Unsupported AI mode")
    .Validate(
        o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var u) && u.Scheme == "https",
        "Foundry requires HTTPS"
    )
    .ValidateOnStart();
builder.Services.AddHttpClient<FoundryVisionProvider>(c => c.Timeout = TimeSpan.FromSeconds(100));
var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<InternalKeyMiddleware>();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.Run();

public partial class AiProgram;
