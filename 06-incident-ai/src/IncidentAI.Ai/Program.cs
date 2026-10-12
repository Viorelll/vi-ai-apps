using System.Security.Cryptography;
using System.Text;
using IncidentAI.Ai.Providers;
using IncidentAI.Application.Contracts;
using IncidentAI.Application.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<OfflineDiagnosisProvider>();
builder.Services.AddHttpClient<FoundryDiagnosisProvider>(http => http.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddScoped<IDiagnosisProvider>(sp => sp.GetRequiredService<FoundryDiagnosisProvider>());
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "IncidentAI.Ai" }));
app.MapPost("/internal/diagnose", async (HttpRequest request, DiagnosisInput input, IDiagnosisProvider provider, IConfiguration config, CancellationToken ct) =>
{
    var expected = config["Internal:Key"] ?? "local-development-key";
    var supplied = request.Headers["X-Internal-Key"].ToString();
    var a = Encoding.UTF8.GetBytes(expected);
    var b = Encoding.UTF8.GetBytes(supplied);
    if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b)) return Results.Unauthorized();
    if (input.ToUtc <= input.FromUtc || input.ToUtc - input.FromUtc > TimeSpan.FromHours(24) || input.Events.Count > 100)
        return Results.BadRequest(new { error = "invalid_snapshot" });
    var result = await provider.DiagnoseAsync(input, ct);
    var allowed = input.Events.Select(x => x.SourceId).ToHashSet(StringComparer.Ordinal);
    if (result.Hypotheses.Any(h => h.EvidenceIds.Any(id => !allowed.Contains(id)))) return Results.Problem("Invalid evidence source.", statusCode: 502);
    return Results.Ok(result);
});
app.Run();
