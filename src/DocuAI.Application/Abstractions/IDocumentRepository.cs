using DocuAI.Domain.Entities;

namespace DocuAI.Application.Abstractions;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken);
    Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Document?> FindByHashAsync(string sha256, CancellationToken cancellationToken);
    Task AddAsync(Document document, IngestionJob job, CancellationToken cancellationToken);
    Task RetryAsync(Document document, IngestionJob job, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
