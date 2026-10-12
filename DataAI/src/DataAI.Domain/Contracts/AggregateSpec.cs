namespace DataAI.Domain.Contracts;

public sealed record AggregateSpec(string Metric, string GroupBy, DateOnly From, DateOnly ToExclusive, string Currency);
