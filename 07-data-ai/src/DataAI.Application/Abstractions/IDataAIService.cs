using DataAI.Domain.Contracts;

namespace DataAI.Application.Abstractions;

public interface IDataAIService
{
    Task<IReadOnlyList<DatasetSummary>> GetDatasetsAsync(CancellationToken cancellationToken);
    Task<ImportResult> ImportCsvAsync(string name, Stream csv, CancellationToken cancellationToken);
    Task<AggregateResult> AnalyzeAsync(AnalyzeRequest request, CancellationToken cancellationToken);
    Task<AggregateResult?> GetRunAsync(Guid runId, CancellationToken cancellationToken);
}
