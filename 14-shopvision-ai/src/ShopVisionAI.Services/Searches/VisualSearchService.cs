using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Database;
using ShopVisionAI.Domain.Entities;
using ShopVisionAI.Services.Errors;

namespace ShopVisionAI.Services.Searches;

public sealed class VisualSearchService(
    AppDbContext db,
    ShopVisionAI.Services.Clients.DomainAiClient ai
)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<VisualSearch> CreateAsync(CreateSearchRequest request, CancellationToken ct)
    {
        if (request.CatalogVersion != "seed-v1")
            throw new DomainException("catalog_version_conflict", 409);
        if (request.Question?.Length > 2000)
            throw new DomainException("question_too_long");
        var doc = await db.Documents.FindAsync([request.ImageDocumentId], ct);
        if (doc == null || doc.Status != "Ready")
            throw new DomainException("image_not_ready", 409);
        var s = new VisualSearch { ImageDocumentId = doc.Id, Question = request.Question };
        db.VisualSearches.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    public async Task<VisualSearch> FindAsync(Guid id, CancellationToken ct) =>
        await db.VisualSearches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new DomainException("search_not_found", 404);

    public async Task<IngestionJob> AnalyzeAsync(
        Guid id,
        AnalyzeRequest request,
        CancellationToken ct
    )
    {
        if (request.RequestId == Guid.Empty)
            throw new DomainException("request_id_required");
        var previous = await db.IngestionJobs.FirstOrDefaultAsync(
            x => x.TargetId == id && x.RequestId == request.RequestId,
            ct
        );
        if (previous != null)
            return previous;
        var s =
            await db
                .VisualSearches.Include(x => x.ImageDocument)
                .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("search_not_found", 404);
        if (
            s.Revision != request.Revision
            || s.State is not ("Created" or "Failed" or "Completed" or "NeedsInput")
            || s.ImageDocument.Status != "Ready"
        )
            throw new DomainException("revision_or_state_conflict", 409);
        s.State = "Inspecting";
        s.Revision++;
        s.ErrorCode = null;
        s.ResultJson = null;
        var job = new IngestionJob { TargetId = id, RequestId = request.RequestId };
        db.IngestionJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<QuestionResponse> QuestionAsync(
        Guid id,
        QuestionRequest request,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.Question)
            || request.Question.Length > 2000
            || request.RequestId == Guid.Empty
        )
            throw new DomainException("invalid_question");
        var previous = await db
            .ConversationMessages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SearchId == id && x.RequestId == request.RequestId, ct);
        var s = await FindAsync(id, ct);
        if (s.State != "Completed" || s.ResultJson == null)
            throw new DomainException("search_not_completed", 409);
        var result = JsonSerializer.Deserialize<SearchResult>(s.ResultJson, Json)!;
        var sources = result.Candidates.SelectMany(x => x.SourceIds).Distinct().ToArray();
        if (previous != null)
            return new(previous.Answer, sources);
        if (s.InputTokens + s.OutputTokens >= 14000)
            throw new DomainException("token_budget_exceeded", 409);
        var generated = await ai.GenerateAsync(
            new(request.RequestId, request.Question, result.Observations, result.Candidates),
            ct
        );
        ShopVisionAI.Contracts.Validation.GroundingValidator.Validate(generated, result.Candidates);
        if (s.InputTokens + s.OutputTokens + generated.InputTokens + generated.OutputTokens > 15000)
            throw new DomainException("token_budget_exceeded", 409);
        var answer = generated.Answer;
        sources = generated.SourceIds.ToArray();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db
            .VisualSearches.Where(x =>
                x.Id == id && x.Revision == s.Revision && x.State == "Completed"
            )
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(v => v.Revision, v => v.Revision + 1)
                        .SetProperty(v => v.InputTokens, v => v.InputTokens + generated.InputTokens)
                        .SetProperty(
                            v => v.OutputTokens,
                            v => v.OutputTokens + generated.OutputTokens
                        ),
                ct
            );
        if (updated == 0)
            throw new DomainException("revision_conflict", 409);
        db.ConversationMessages.Add(
            new ConversationMessage
            {
                SearchId = id,
                RequestId = request.RequestId,
                Question = request.Question,
                Answer = answer,
            }
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(answer, sources);
    }
}
