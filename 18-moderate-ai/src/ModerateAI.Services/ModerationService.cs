using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModerateAI.Database;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Entities;
using ModerateAI.Domain.Models;

namespace ModerateAI.Services;

public sealed class ModerationService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<ModerationItem> CreateAsync(CreateItemRequest request, CancellationToken ct)
    {
        if (
            request.RequestId == Guid.Empty
            || (string.IsNullOrWhiteSpace(request.Text) && request.ImageDocumentId == null)
        )
            throw new DomainException(400, "input_required");
        if (request.Text?.EnumerateRunes().Count() > 10000)
            throw new DomainException(400, "text_too_long");
        await using var db = await factory.CreateDbContextAsync(ct);
        string? imageHash = null;
        if (request.ImageDocumentId is Guid imageId)
        {
            var doc = await db.Documents.SingleOrDefaultAsync(x => x.Id == imageId, ct);
            if (doc?.Status != "Ready")
                throw new DomainException(409, "image_not_ready");
            imageHash = doc.Sha256;
        }
        var hash = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            request.Text,
                            request.ImageDocumentId,
                            imageHash,
                        }
                    )
                )
            )
        );
        var existing = await db.ModerationItems.SingleOrDefaultAsync(
            x => x.RequestId == request.RequestId,
            ct
        );
        if (existing != null)
        {
            if (existing.InputHash != hash)
                throw new DomainException(409, "request_id_reused");
            return existing;
        }
        var item = new ModerationItem
        {
            RequestId = request.RequestId,
            Text = request.Text,
            ImageDocumentId = request.ImageDocumentId,
            InputHash = hash,
        };
        db.Add(item);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e)
            when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            existing = await db.ModerationItems.SingleAsync(
                x => x.RequestId == request.RequestId,
                ct
            );
            if (existing.InputHash != hash)
                throw new DomainException(409, "request_id_reused");
            return existing;
        }
        return item;
    }

    public async Task<Guid> AnalyzeAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item =
            await db.ModerationItems.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException(404, "item_not_found");
        if (item.Stale)
            throw new DomainException(409, "source_deleted");
        var prior = await db
            .IngestionJobs.Where(x => x.TargetId == id && x.Kind == "Analyze")
            .OrderByDescending(x => x.NextAttemptAtUtc)
            .FirstOrDefaultAsync(ct);
        if (item.State == "Analyzing" && prior != null)
            return prior.Id;
        if (item.State is not ("Submitted" or "Failed"))
            throw new DomainException(409, "already_analyzed");
        var job = new IngestionJob { TargetId = id };
        item.State = "Analyzing";
        item.ErrorCode = null;
        item.Revision++;
        db.Add(job);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(409, "revision_conflict");
        }
        return job.Id;
    }

    public async Task<ItemResult> GetAsync(Guid id, bool includeInput, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item =
            await db.ModerationItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException(404, "item_not_found");
        var analysis = await db
            .SafetyAnalyses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ItemId == id, ct);
        var scores =
            analysis == null
                ? []
                : await db
                    .CategoryFindings.AsNoTracking()
                    .Where(x => x.AnalysisId == analysis.Id)
                    .OrderBy(x => x.Category)
                    .Select(x => new SafetyScore(x.Category, x.Severity))
                    .ToArrayAsync(ct);
        var policy = await db
            .PolicyDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ItemId == id, ct);
        var reviews = await db
            .ReviewDecisions.AsNoTracking()
            .Where(x => x.ItemId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToArrayAsync(ct);
        return new(
            item.Id,
            item.Revision,
            item.State,
            item.Stale,
            item.ErrorCode,
            item.Explanation,
            item.CreatedAtUtc,
            includeInput && !item.Stale ? item.Text : null,
            item.Stale ? null : item.ImageDocumentId,
            analysis == null
                ? null
                : new
                {
                    analysis.Provider,
                    analysis.ApiVersion,
                    analysis.OutputScale,
                    analysis.Complete,
                    analysis.InputTokens,
                    analysis.OutputTokens,
                    Scores = scores,
                    RawUrl = $"/api/moderation/items/{id}/raw",
                },
            policy == null
                ? null
                : new
                {
                    policy.Decision,
                    policy.PolicyVersion,
                    policy.DecisionHash,
                    Reasons = JsonSerializer.Deserialize<string[]>(policy.ReasonsJson),
                },
            reviews
        );
    }

    public async Task<object> QueueAsync(int page, int pageSize, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.ModerationItems.AsNoTracking().Where(x => x.State == "Review" && !x.Stale);
        return new
        {
            Total = await query.CountAsync(ct),
            Page = page,
            PageSize = pageSize,
            Items = await query
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.Revision,
                    x.State,
                    x.CreatedAtUtc,
                })
                .ToArrayAsync(ct),
        };
    }

    public async Task ReviewAsync(Guid id, ReviewRequest request, CancellationToken ct)
    {
        if (request.Decision is not ("allow" or "block") || request.Note?.Length > 1000)
            throw new DomainException(400, "invalid_review");
        await using var db = await factory.CreateDbContextAsync(ct);
        var item =
            await db.ModerationItems.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException(404, "item_not_found");
        if (item.Stale || item.State != "Review" || item.Revision != request.Revision)
            throw new DomainException(409, "revision_conflict");
        item.State = request.Decision == "allow" ? "Allowed" : "Blocked";
        item.Revision++;
        db.Add(
            new ReviewDecision
            {
                ItemId = id,
                ExpectedRevision = request.Revision,
                Decision = request.Decision,
                Note = request.Note,
            }
        );
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException(409, "revision_conflict");
        }
        catch (DbUpdateException e)
            when (e.InnerException
                    is Npgsql.PostgresException
                    {
                        SqlState: "23505",
                        ConstraintName: "IX_ReviewDecisions_ItemId_ExpectedRevision"
                    }
            )
        {
            throw new DomainException(409, "revision_conflict");
        }
    }
}
