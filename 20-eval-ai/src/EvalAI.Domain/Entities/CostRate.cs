namespace EvalAI.Domain.Entities;

public sealed class CostRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeploymentAlias { get; set; } = ""; public string RateVersion { get; set; } = ""; public string Currency { get; set; } = "USD"; public decimal? InputPerMillion { get; set; }
    public decimal? OutputPerMillion { get; set; }
    public DateTimeOffset EffectiveAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
