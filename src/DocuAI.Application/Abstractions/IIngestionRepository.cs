using DocuAI.Domain.Entities;

namespace DocuAI.Application.Abstractions;

public interface IIngestionRepository
{
    Task<(IngestionJob Job, Document Document)?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task CompleteAsync(IngestionJob job, Document document, IReadOnlyCollection<DocumentChunk> chunks, CancellationToken cancellationToken);
    Task FailAsync(IngestionJob job, Document document, string errorCode, CancellationToken cancellationToken);
}
