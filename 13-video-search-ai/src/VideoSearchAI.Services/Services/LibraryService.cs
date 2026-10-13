using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Entities;
using VideoSearchAI.Domain.Models;

namespace VideoSearchAI.Services.Services;

public sealed class LibraryService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<VideoDto[]> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await (
            from v in db.Videos
            join d in db.Documents on v.DocumentId equals d.Id
            where !d.Deleted
            orderby d.CreatedAtUtc descending
            select new VideoDto(v.Id, d.Id, v.Title, d.DurationMs, v.State, v.Revision, v.ErrorCode)
        ).ToArrayAsync(ct);
    }

    public async Task<VideoDto> CreateAsync(CreateVideoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
            throw new DomainException("invalid_title");
        await using var db = await factory.CreateDbContextAsync(ct);
        var doc =
            await db.Documents.SingleOrDefaultAsync(
                x => x.Id == request.DocumentId && !x.Deleted,
                ct
            ) ?? throw new DomainException("document_not_found", 404);
        var video = await db.Videos.SingleOrDefaultAsync(x => x.DocumentId == doc.Id, ct);
        if (video != null)
            return new(
                video.Id,
                doc.Id,
                video.Title,
                doc.DurationMs,
                video.State,
                video.Revision,
                video.ErrorCode
            );
        video = new VideoEntry { DocumentId = doc.Id, Title = request.Title.Trim() };
        db.Videos.Add(video);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e)
            when (e.InnerException
                    is Npgsql.PostgresException
                    {
                        SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
                    }
            )
        {
            db.ChangeTracker.Clear();
            video = await db.Videos.SingleAsync(x => x.DocumentId == doc.Id, ct);
        }
        return new(
            video.Id,
            doc.Id,
            video.Title,
            doc.DurationMs,
            video.State,
            video.Revision,
            video.ErrorCode
        );
    }

    public async Task<Guid> IndexAsync(Guid id, long? revision, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var v =
            await db.Videos.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("video_not_found", 404);
        var d = await db.Documents.SingleAsync(x => x.Id == v.DocumentId, ct);
        if (d.Deleted)
            throw new DomainException("document_deleted", 409);
        if (revision.HasValue && revision != v.Revision)
            throw new DomainException("revision_conflict", 409);
        if (v.State is "Analyzing" or "Indexing")
            throw new DomainException("already_processing", 409);
        var generation =
            await db
                .Jobs.Where(x => x.TargetId == id && x.Kind == "Index")
                .Select(x => (int?)x.Generation)
                .MaxAsync(ct) ?? 0;
        var job = new IngestionJob
        {
            DocumentId = d.Id,
            TargetId = id,
            Generation = generation + 1,
        };
        v.State = "Analyzing";
        v.ErrorCode = null;
        v.Revision++;
        d.Status = "Processing";
        db.Jobs.Add(job);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("revision_conflict", 409);
        }
        return job.Id;
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == documentId, ct)
            ?? throw new DomainException("document_not_found", 404);
        if (d.Deleted)
            return;
        d.Deleted = true;
        d.Status = "Deleting";
        var v = await db.Videos.SingleOrDefaultAsync(x => x.DocumentId == documentId, ct);
        if (v != null)
        {
            v.State = "Stale";
            v.Revision++;
        }
        var runs = await db
            .Runs.Where(x => db.Citations.Any(c => c.RunId == x.Id && c.DocumentId == documentId))
            .ToListAsync(ct);
        foreach (var run in runs)
            run.State = "Stale";
        db.Jobs.Add(
            new IngestionJob
            {
                DocumentId = documentId,
                TargetId = documentId,
                Kind = "Cleanup",
            }
        );
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> RetryDocumentAsync(Guid documentId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var v =
            await db.Videos.SingleOrDefaultAsync(x => x.DocumentId == documentId, ct)
            ?? throw new DomainException("video_not_found", 404);
        if (v.State != "Failed")
            throw new DomainException("retry_requires_failed_video", 409);
        return await IndexAsync(v.Id, v.Revision, ct);
    }
}
