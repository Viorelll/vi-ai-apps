using TutorAI.Domain.DTOs;
using TutorAI.Infrastructure;
using TutorAI.Infrastructure.Ai;
using TutorAI.Infrastructure.Extraction;
using TutorAI.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStorage(builder.Configuration).AddProviders();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<InternalKeyMiddleware>();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapPost(
    "/internal/domain/step",
    (AiRequest r, FoundryClient client, CancellationToken ct) => client.ExecuteAsync(r, ct)
);
app.MapPost(
    "/internal/extractions/start",
    (ExtractionStart r, DocumentExtractor extractor, CancellationToken ct) =>
        extractor.StartAsync(r, ct)
);
app.MapPost(
    "/internal/extractions/poll",
    (ExtractionPoll r, DocumentExtractor extractor, CancellationToken ct) =>
        extractor.PollAsync(r, ct)
);
await app.RunAsync();
