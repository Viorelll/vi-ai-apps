using InvoiceAI.Domain.Entities;
namespace InvoiceAI.Services.Abstractions;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct);
    Task<Document?> FindHashAsync(string hash, CancellationToken ct);
    Task<Document> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(Document document, CancellationToken ct);
    Task<Guid> DeleteAsync(Guid id, CancellationToken ct);
    Task RetryAsync(Guid id, CancellationToken ct);
}
