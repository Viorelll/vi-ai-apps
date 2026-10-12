using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Models;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Entities;
using ProcureAI.Domain.Models;

namespace ProcureAI.Database.Persistence;

public sealed class ProcurementRepository(IDbContextFactory<AppDbContext> factory) : IProcurementRepository
{
    public async Task<ProcurementRun> CreateAsync(CreateRunRequest request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = new ProcurementRun { Brief = request.Brief.Trim(), CriteriaJson = JsonDefaults.Serialize(request.Criteria), BudgetTokens = request.BudgetTokens };
        db.Runs.Add(run); await db.SaveChangesAsync(ct); return run;
    }

    public async Task<IReadOnlyList<RunSummaryDto>> ListRunsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Runs.OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new RunSummaryDto(x.Id, x.Brief, x.State, x.Revision, x.Stale)).ToListAsync(ct);
    }

    public async Task<RunViewDto?> GetRunAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.Runs.FindAsync([id], ct);
        if (run is null) return null;
        var links = await db.RunDocuments.Where(x => x.RunId == id).Select(x => x.DocumentId).ToArrayAsync(ct);
        var docs = await db.Documents.Where(x => links.Contains(x.Id)).ToListAsync(ct);
        var steps = await db.Steps.Where(x => x.RunId == id).OrderBy(x => x.CompletedAtUtc)
            .Select(x => new StepViewDto(x.Role, x.State, x.Deployment, x.InputTokens, x.OutputTokens, x.DurationMs)).ToListAsync(ct);
        var recommendation = await db.Recommendations.SingleOrDefaultAsync(x => x.RunId == id && x.SnapshotHash == run.SnapshotHash, ct);
        var offers = await db.Offers.Where(x => x.RunId == id).OrderBy(x => x.SupplierLabel).ToListAsync(ct);
        return new RunViewDto(id, run.Brief, JsonDefaults.Deserialize<CriteriaDto>(run.CriteriaJson), run.State, run.Revision,
            run.Stale, run.ErrorCode, run.SnapshotHash, run.BudgetTokens, run.UsedTokens, docs.Select(DocumentDto.From).ToArray(), steps, offers.Select(ToDto).ToArray(),
            recommendation is null ? [] : JsonDefaults.Deserialize<IReadOnlyList<OfferScoreDto>>(recommendation.ScoresJson),
            recommendation is null ? null : JsonDefaults.Deserialize<RecommendationDto>(recommendation.ReportJson),
            await SourcesAsync(db, links, ct));
    }

    public async Task AttachAsync(Guid id, AttachDocumentRequest request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await RequireRunAsync(db, id, ct);
        if (run.State != "Created" || run.Revision != request.Revision) throw new AppException("revision_conflict", 409);
        if (request.Role is not ("offer" or "policy" or "specification")) throw new AppException("invalid_document_role");
        var document = await db.Documents.FindAsync([request.DocumentId], ct);
        if (document is null || document.State == "Deleting") throw new AppException("document_not_found", 404);
        if (await db.RunDocuments.AnyAsync(x => x.RunId == id && x.DocumentId == request.DocumentId, ct))
            throw new AppException("document_already_attached", 409);
        if (await db.RunDocuments.CountAsync(x => x.RunId == id, ct) >= 20) throw new AppException("document_limit", 422);
        db.RunDocuments.Add(new RunDocument { RunId = id, DocumentId = request.DocumentId, Role = request.Role });
        run.Revision++;
        await SaveAsync(db, ct);
    }

    public async Task<Guid> StartAsync(Guid id, long revision, bool retry, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await RequireRunAsync(db, id, ct);
        if (run.Revision != revision || run.Stale || (retry ? run.State != "Failed" : run.State != "Created"))
            throw new AppException("revision_or_state_conflict", 409);
        var links = await db.RunDocuments.Where(x => x.RunId == id).OrderBy(x => x.DocumentId).ToListAsync(ct);
        if (!links.Any(x => x.Role == "offer")) throw new AppException("offers_required", 422);
        var ids = links.Select(x => x.DocumentId).ToArray();
        if (await db.Documents.CountAsync(x => ids.Contains(x.Id) && x.State == "Ready", ct) != ids.Length)
            throw new AppException("documents_not_ready", 409);
        var sources = await SourcesAsync(db, ids, ct);
        var snapshot = JsonDefaults.Serialize(new { run.Brief, run.CriteriaJson, links, sources });
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
        if (retry && hash != run.SnapshotHash) throw new AppException("snapshot_changed", 409);
        run.SnapshotHash = hash; run.State = "Planning"; run.ErrorCode = null; run.Revision++;
        var job = retry ? await db.Jobs.SingleAsync(x => x.RunId == id, ct) : new DurableJob { RunId = id, Kind = "Run" };
        job.State = "Queued"; job.Attempt = 0; job.ErrorCode = null; job.LeaseToken = null; job.LeaseUntilUtc = null;
        job.NextAttemptAtUtc = DateTimeOffset.UtcNow;
        if (!retry) db.Jobs.Add(job);
        await SaveAsync(db, ct); return job.Id;
    }

    public async Task<WorkflowSnapshot> SnapshotAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await RequireRunAsync(db, id, ct);
        var links = await db.RunDocuments.Where(x => x.RunId == id).ToListAsync(ct);
        var sources = await SourcesAsync(db, links.Select(x => x.DocumentId).ToArray(), ct);
        var steps = await db.Steps.Where(x => x.RunId == id).ToListAsync(ct);
        var offers = await db.Offers.Where(x => x.RunId == id).ToListAsync(ct);
        return new WorkflowSnapshot(run, steps, sources, offers.Select(ToDto).ToArray(), [], links.Where(x => x.Role == "offer").Select(x => x.DocumentId).ToArray());
    }

    public async Task CommitStepAsync(DurableJob job, AgentStep step, IReadOnlyList<OfferDto>? offers,
        RecommendationDto? recommendation, IReadOnlyList<OfferScoreDto> scores, string state, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await OwnedJobAsync(db, job, ct);
        if (current is null) return;
        var run = await RequireRunAsync(db, job.RunId!.Value, ct);
        if (run.Stale) throw new AppException("sources_changed", 422);
        if (await db.Steps.AnyAsync(x => x.RunId == run.Id && x.Role == step.Role, ct)) return;
        db.Steps.Add(step);
        run.UsedTokens += step.InputTokens + step.OutputTokens;
        run.State = state; run.Revision++;
        run.ErrorCode = state == "Failed" ? "token_budget_exceeded" : state == "NeedsInput" ? "incomplete_agent_result" : null;
        if (offers is not null)
            db.Offers.AddRange(offers.Select(x => new Offer
            {
                Id = x.Id,
                RunId = run.Id,
                DocumentId = x.DocumentId,
                SupplierLabel = x.SupplierLabel,
                Currency = x.Currency,
                Quantity = x.Quantity,
                Unit = x.Unit,
                Net = x.Net,
                Shipping = x.Shipping,
                LeadDays = x.LeadDays,
                MeetsRequirements = x.MeetsRequirements,
                EvidenceJson = JsonDefaults.Serialize(x.SourceIds)
            }));
        if (recommendation is not null)
            db.Recommendations.Add(new Recommendation
            {
                RunId = run.Id,
                SnapshotHash = run.SnapshotHash,
                ScoresJson = JsonDefaults.Serialize(scores),
                ReportJson = JsonDefaults.Serialize(recommendation)
            });
        current.State = state is "Completed" or "NeedsInput" ? "Complete" : state == "Failed" ? "Failed" : "Queued";
        current.LeaseToken = null; current.LeaseUntilUtc = null; current.Attempt = 0;
        current.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(500);
        await SaveAsync(db, ct); await transaction.CommitAsync(ct);
    }

    public async Task StopRunAsync(DurableJob job, string state, string code, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await OwnedJobAsync(db, job, ct);
        if (current is null) return;
        var run = await RequireRunAsync(db, job.RunId!.Value, ct);
        run.State = state; run.ErrorCode = code; run.Revision++;
        current.State = "Failed"; current.ErrorCode = code; current.LeaseToken = null; current.LeaseUntilUtc = null;
        await SaveAsync(db, ct);
    }

    public async Task<Document?> GetDocumentAsync(Guid id, CancellationToken ct)
    { await using var db = await factory.CreateDbContextAsync(ct); return await db.Documents.FindAsync([id], ct); }

    public async Task<IReadOnlyList<Document>> ListDocumentsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Documents.Where(x => x.State != "Deleting").OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
    }

    public async Task<Document?> FindDocumentAsync(string hash, CancellationToken ct)
    { await using var db = await factory.CreateDbContextAsync(ct); return await db.Documents.SingleOrDefaultAsync(x => x.Sha256 == hash && x.State != "Deleting", ct); }

    public async Task<Document> AddDocumentAsync(Document document, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Documents.Add(document); db.Jobs.Add(new DurableJob { DocumentId = document.Id, Kind = "Extract" });
        try { await db.SaveChangesAsync(ct); return document; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            await using var retry = await factory.CreateDbContextAsync(ct);
            return await retry.Documents.SingleAsync(x => x.Sha256 == document.Sha256 && x.State != "Deleting", ct);
        }
    }

    public async Task RetryDocumentAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var document = await db.Documents.FindAsync([id], ct) ?? throw new AppException("document_not_found", 404);
        if (document.State != "Failed") throw new AppException("document_not_failed", 409);
        document.State = "Uploaded"; document.ErrorCode = null;
        var job = await db.Jobs.SingleAsync(x => x.DocumentId == id && x.Kind == "Extract", ct);
        job.State = "Queued"; job.Attempt = 0; job.ErrorCode = null; job.ProviderOperation = null; job.NextAttemptAtUtc = DateTimeOffset.UtcNow;
        await SaveAsync(db, ct);
    }

    public async Task DeleteDocumentAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var document = await db.Documents.FindAsync([id], ct) ?? throw new AppException("document_not_found", 404);
        if (document.State == "Deleting") return;
        document.State = "Deleting";
        var runIds = await db.RunDocuments.Where(x => x.DocumentId == id).Select(x => x.RunId).ToArrayAsync(ct);
        var runs = await db.Runs.Where(x => runIds.Contains(x.Id)).ToListAsync(ct);
        foreach (var run in runs) { run.Stale = true; run.Revision++; }
        var jobs = await db.Jobs.Where(x => x.DocumentId == id || (x.RunId != null && runIds.Contains(x.RunId.Value))).ToListAsync(ct);
        foreach (var job in jobs) { job.State = "Complete"; job.LeaseToken = null; job.LeaseUntilUtc = null; }
        db.Jobs.Add(new DurableJob { DocumentId = id, Kind = "Cleanup" });
        await db.Chunks.Where(x => x.DocumentId == id).ExecuteDeleteAsync(ct);
        await SaveAsync(db, ct); await transaction.CommitAsync(ct);
    }

    public async Task CompleteDocumentAsync(DurableJob job, string text, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var current = await OwnedJobAsync(db, job, ct);
        if (current is null) return;
        var document = await db.Documents.FindAsync([job.DocumentId!.Value], ct);
        if (document is null || document.State == "Deleting") return;
        document.State = "Ready"; document.ActiveGeneration = 1; document.ErrorCode = null;
        // One bounded source per offer avoids splitting structured JSON fields across chunk boundaries.
        db.Chunks.Add(new DocumentChunk { DocumentId = document.Id, Text = text });
        current.State = "Complete"; current.LeaseToken = null; current.LeaseUntilUtc = null;
        await SaveAsync(db, ct);
    }

    public async Task<Guid> SaveChatAsync(ChatRequest request, string answer, IReadOnlyList<SourceDto> sources,
        DomainStepResult result, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Conversation conversation;
        if (request.ConversationId is { } id)
        {
            conversation = await db.Conversations.FindAsync([id], ct) ?? throw new AppException("conversation_not_found", 404);
            if (conversation.RunId != request.RunId) throw new AppException("conversation_scope_mismatch", 409);
        }
        else { conversation = new Conversation { RunId = request.RunId }; db.Conversations.Add(conversation); }
        db.Messages.Add(new Message { ConversationId = conversation.Id, Role = "user", Text = request.Question });
        var message = new Message { ConversationId = conversation.Id, Role = "assistant", Text = answer, InputTokens = result.InputTokens, OutputTokens = result.OutputTokens };
        db.Messages.Add(message);
        db.Citations.AddRange(sources.Select(s => new Citation
        {
            MessageId = message.Id,
            DocumentId = s.DocumentId,
            ChunkId = s.SourceId,
            Label = s.SourceId.ToString(),
            FileName = s.FileName,
            Excerpt = s.Text[..Math.Min(500, s.Text.Length)],
            Generation = s.Generation,
            PageNumber = s.PageNumber
        }));
        await SaveAsync(db, ct); return conversation.Id;
    }

    private static OfferDto ToDto(Offer x) => new(x.Id, x.DocumentId, x.SupplierLabel, x.Currency, x.Quantity, x.Unit, x.Net,
        x.Shipping, x.LeadDays, x.MeetsRequirements, JsonDefaults.Deserialize<IReadOnlyList<Guid>>(x.EvidenceJson));

    private static async Task<IReadOnlyList<SourceDto>> SourcesAsync(AppDbContext db, Guid[] ids, CancellationToken ct) =>
        await (from chunk in db.Chunks
               join document in db.Documents on chunk.DocumentId equals document.Id
               where ids.Contains(document.Id) && document.State == "Ready" && chunk.Generation == document.ActiveGeneration
               orderby document.Id, chunk.Ordinal
               select new SourceDto(document.Id, chunk.Id, document.FileName, chunk.Generation, chunk.Text, chunk.PageNumber)).ToListAsync(ct);

    private static async Task<ProcurementRun> RequireRunAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.Runs.FindAsync([id], ct) ?? throw new AppException("run_not_found", 404);

    private static Task<DurableJob?> OwnedJobAsync(AppDbContext db, DurableJob job, CancellationToken ct) =>
        db.Jobs.SingleOrDefaultAsync(x => x.Id == job.Id && x.LeaseToken == job.LeaseToken && x.LeaseUntilUtc > DateTimeOffset.UtcNow, ct);

    private static async Task SaveAsync(AppDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AppException("revision_conflict", 409); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        { throw new AppException("duplicate_resource", 409); }
    }
}
