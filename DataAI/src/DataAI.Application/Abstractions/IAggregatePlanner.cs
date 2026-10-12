using DataAI.Domain.Contracts;

namespace DataAI.Application.Abstractions;

public interface IAggregatePlanner
{
    Task<AggregateSpec> PlanAsync(string question, IReadOnlyCollection<string> categories, CancellationToken cancellationToken);
    Task<string> ExplainAsync(string question, AggregateSpec spec, IReadOnlyList<AggregateBucket> rows, decimal total, CancellationToken cancellationToken);
    bool IsConfigured { get; }
}
