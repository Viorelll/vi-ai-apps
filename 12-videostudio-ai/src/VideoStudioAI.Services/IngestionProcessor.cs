using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Database;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Services;

public sealed class IngestionProcessor(
    IDbContextFactory<AppDbContext> factory,
    IObjectStore store,
    IAiGateway ai,
    ILogger<IngestionProcessor> logger
)
{
    public async Task ProcessAsync(IngestionJob claimed, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var job = await db.IngestionJobs.SingleAsync(j => j.Id == claimed.Id, ct);
        if (job.LeaseToken != claimed.LeaseToken)
            return;
        var doc = await db.Documents.SingleAsync(d => d.Id == job.DocumentId, ct);
        try
        {
            if (job.Kind == "Delete")
            {
                await store.DeleteAsync(doc.ObjectKey, ct);
                db.Chunks.RemoveRange(
                    await db.Chunks.Where(c => c.DocumentId == doc.Id).ToArrayAsync(ct)
                );
                doc.Text = null;
                job.State = "Ready";
            }
            else
            {
                doc.Status = "Processing";
                await db.SaveChangesAsync(ct);
                await using var content = await store.DownloadAsync(doc.ObjectKey, ct);
                var result = await ai.ExtractAsync(
                    new ExtractRequest(
                        doc.ContentType,
                        Convert.ToBase64String(await File.ReadAllBytesAsync(content.Path, ct))
                    ),
                    ct
                );
                if (string.IsNullOrWhiteSpace(result.Text) || result.Text.Length > 200000)
                    throw new AppException(
                        422,
                        "extraction_invalid",
                        "Extraction must contain 1–200,000 characters."
                    );
                doc.Text = result.Text;
                db.Chunks.RemoveRange(
                    await db.Chunks.Where(c => c.DocumentId == doc.Id).ToArrayAsync(ct)
                );
                for (
                    int offset = 0, ordinal = 0;
                    offset < result.Text.Length;
                    offset += 1200, ordinal++
                )
                    db.Add(
                        new DocumentChunk
                        {
                            DocumentId = doc.Id,
                            Ordinal = ordinal,
                            Text = result.Text.Substring(
                                offset,
                                Math.Min(1200, result.Text.Length - offset)
                            ),
                        }
                    );
                doc.Status = "Ready";
                doc.ErrorCode = null;
                job.State = "Ready";
            }
        }
        catch (Exception ex) when (ex is not DbUpdateConcurrencyException)
        {
            job.Attempt++;
            doc.ErrorCode = (ex as AppException)?.Code ?? "extraction_failed";
            if (job.Attempt < 3 && (ex is not AppException e || e.Status >= 500))
            {
                job.NextPollAtUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5 * job.Attempt);
            }
            else
            {
                job.State = "Failed";
                if (job.Kind != "Delete")
                    doc.Status = "Failed";
            }
            logger.LogWarning(ex, "Ingestion {JobId} failed", job.Id);
        }
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Ingestion lease lost {JobId}", job.Id);
        }
    }
}
