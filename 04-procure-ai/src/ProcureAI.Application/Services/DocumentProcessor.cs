using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Entities;

namespace ProcureAI.Application.Services;

public sealed class DocumentProcessor(IProcurementRepository repository, IObjectStore objects, IAiGateway ai, IJobStore jobs)
{
    public async Task ProcessAsync(DurableJob job, CancellationToken ct)
    {
        var document = await repository.GetDocumentAsync(job.DocumentId!.Value, ct);
        if (document is null || document.State == "Deleting")
        {
            await jobs.ReleaseAsync(job, "Complete", TimeSpan.Zero, null, null, ct);
            return;
        }
        if (job.Kind == "Cleanup")
        {
            await objects.DeleteAsync(document.ObjectKey, ct);
            await objects.DeleteAsync($"artifacts/{document.Id}/cu-v1/raw.json", ct);
            await jobs.ReleaseAsync(job, "Complete", TimeSpan.Zero, null, null, ct);
            return;
        }
        string text;
        if (document.ContentType is "text/plain" or "text/markdown" or "application/json")
        {
            await using var stream = await objects.OpenAsync(document.ObjectKey, ct);
            using var reader = new StreamReader(stream);
            text = await reader.ReadToEndAsync(ct);
        }
        else
        {
            var result = await ai.ExtractAsync(new ExtractionRequest(document.Id, document.ObjectKey, document.FileName, job.ProviderOperation), ct);
            if (result.Status == "running")
            {
                if (string.IsNullOrWhiteSpace(result.ProviderOperation)) throw new AppException("invalid_extraction", 422);
                await jobs.ReleaseAsync(job, "Queued", TimeSpan.FromSeconds(3), null, result.ProviderOperation, ct);
                return;
            }
            text = result.Text ?? "";
        }
        if (string.IsNullOrWhiteSpace(text)) throw new AppException("empty_extraction", 422);
        if (text.Length > 200000) throw new AppException("extraction_limit", 422);
        await repository.CompleteDocumentAsync(job, text, ct);
    }
}
