namespace BuildBudgetAI.Domain.Interfaces;

public interface IObjectStore
{
    Task InitializeAsync(CancellationToken ct);
    Task PutAsync(string key, Stream data, string contentType, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
