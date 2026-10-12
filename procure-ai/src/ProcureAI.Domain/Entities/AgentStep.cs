namespace ProcureAI.Domain.Entities;

public sealed class AgentStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public required string Role { get; set; }
    public required string InputHash { get; set; }
    public string PromptVersion { get; set; } = "procure-v1";
    public string State { get; set; } = "Succeeded";
    public required string OutputJson { get; set; }
    public string Deployment { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long DurationMs { get; set; }
    public int Attempt { get; set; } = 1;
    public DateTimeOffset CompletedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
