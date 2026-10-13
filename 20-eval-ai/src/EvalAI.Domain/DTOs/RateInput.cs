namespace EvalAI.Domain.DTOs;

public sealed record RateInput(string DeploymentAlias, string RateVersion, string Currency, decimal? InputPerMillion, decimal? OutputPerMillion);
