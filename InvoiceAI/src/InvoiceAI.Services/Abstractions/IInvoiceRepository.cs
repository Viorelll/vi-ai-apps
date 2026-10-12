using InvoiceAI.Domain.Entities;
namespace InvoiceAI.Services.Abstractions;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct);
    Task<Invoice> GetAsync(Guid id, CancellationToken ct);
    Task<Invoice> CreateAsync(Guid documentId, string kind, CancellationToken ct);
    Task SaveAsync(Invoice invoice, long expectedRevision, CancellationToken ct);
    Task<Guid> AnalyzeAsync(Guid id, CancellationToken ct);
    Task<bool> HasDuplicateAsync(Invoice invoice, CancellationToken ct);
    Task<ExportSnapshot> ExportAsync(Invoice invoice, string payload, CancellationToken ct);
}
