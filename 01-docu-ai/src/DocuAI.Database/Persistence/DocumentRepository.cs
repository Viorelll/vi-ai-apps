using DocuAI.Application.Abstractions;
using DocuAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocuAI.Database.Persistence;

public sealed class DocumentRepository(IDbContextFactory<DocuAiDbContext> dbContextFactory)
    : IDocumentRepository, IKnowledgeRepository, IIngestionRepository
{
    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Documents.OrderByDescending(document => document.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Documents.FindAsync([id], cancellationToken);
    }

    public async Task<Document?> FindByHashAsync(string sha256, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Documents.FirstOrDefaultAsync(document => document.Sha256 == sha256, cancellationToken);
    }

    public async Task AddAsync(Document document, IngestionJob job, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Documents.Add(document);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Document document, IngestionJob job, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Documents.Update(document);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var document = await db.Documents.FindAsync([id], cancellationToken);
        if (document is null) return false;
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetReadyChunksAsync(
        IReadOnlyCollection<Guid>? documentIds,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Chunks
            .Include(chunk => chunk.Document)
            .Where(chunk => chunk.Document.Status == DocumentStatus.Ready);

        if (documentIds is not null)
        {
            query = query.Where(chunk => documentIds.Contains(chunk.DocumentId));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<(IngestionJob Job, Document Document)?> ClaimNextAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.Jobs
            .Where(item => (item.Status == IngestionJobStatus.Queued && item.NextAttemptAtUtc <= now) ||
                           (item.Status == IngestionJobStatus.Running && item.LeaseUntilUtc < now))
            .OrderBy(item => item.NextAttemptAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null) return null;

        var document = await db.Documents.FindAsync([job.DocumentId], cancellationToken);
        if (document is null)
        {
            db.Jobs.Remove(job);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        job.Status = IngestionJobStatus.Running;
        job.Attempt++;
        job.LeaseUntilUtc = now.AddMinutes(4);
        document.Status = DocumentStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);
        return (job, document);
    }

    public async Task CompleteAsync(
        IngestionJob job,
        Document document,
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Jobs.Update(job);
        db.Documents.Update(document);
        db.Chunks.AddRange(chunks);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        IngestionJob job,
        Document document,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        job.Status = IngestionJobStatus.Failed;
        job.LeaseUntilUtc = null;
        document.Status = DocumentStatus.Failed;
        document.ErrorCode = errorCode;
        db.Jobs.Update(job);
        db.Documents.Update(document);
        await db.SaveChangesAsync(cancellationToken);
    }
}
