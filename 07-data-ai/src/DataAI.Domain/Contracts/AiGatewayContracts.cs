namespace DataAI.Domain.Contracts.AiGateway;

public sealed record PlanRequest(string Question, IReadOnlyCollection<string> Categories, DateOnly From, DateOnly ToExclusive, string Currency);
public sealed record PlanResponse(string Metric, string GroupBy, DateOnly From, DateOnly ToExclusive, string Currency, bool UsedFoundry);
public sealed record ExplainRequest(string Question, string Metric, string GroupBy, string Currency, IReadOnlyList<AggregateBucket> Rows, decimal Total);
public sealed record ExplainResponse(string Explanation, bool UsedFoundry);
