namespace VideoSearchAI.Domain.Entities;

public sealed class SearchRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string Question { get; set; } = "";
    public string AllowedVideoIdsJson { get; set; } = "[]";
    public string State { get; set; } = "Queued";
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string PromptVersion { get; set; } = "grounded-v1";
    public string InputHash { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
