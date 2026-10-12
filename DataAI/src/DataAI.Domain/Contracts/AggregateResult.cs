namespace DataAI.Domain.Contracts;

public sealed record AggregateBucket(string Label, decimal Value);
public sealed record AggregateResult(Guid RunId, string Metric, string GroupBy, string Currency, IReadOnlyList<AggregateBucket> Rows, decimal Total, string ResultHash, long DatasetRevision, string Explanation, bool UsedFoundry);
public sealed record AnalyzeRequest(Guid DatasetId, string Question);
public sealed record DatasetSummary(Guid Id, string Name, int RowCount, long Revision, string Currency);
public sealed record ImportResult(Guid Id, string Name, int RowsImported, long Revision);
