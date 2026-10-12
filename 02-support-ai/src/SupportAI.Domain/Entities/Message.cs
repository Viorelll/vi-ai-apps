namespace SupportAI.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId
    {
        get; set;
    }
    public Guid? RequestId
    {
        get; set;
    }
    public string Role { get; set; } = "user";
    public required string Text
    {
        get; set;
    }
    public bool Stale
    {
        get; set;
    }
    public long InputTokens
    {
        get; set;
    }
    public long OutputTokens
    {
        get; set;
    }
    public long DurationMs
    {
        get; set;
    }
    public string? Deployment
    {
        get; set;
    }
    public string PromptVersion { get; set; } = "support-v1";
    public string? InputHash
    {
        get; set;
    }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
