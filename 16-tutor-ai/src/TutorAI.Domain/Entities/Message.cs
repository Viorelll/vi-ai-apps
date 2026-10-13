namespace TutorAI.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "assistant";
    public string Text { get; set; } = "";
    public Guid OperationId { get; set; }
    public string Deployment { get; set; } = "";
    public string PromptVersion { get; set; } = "tutor-v1";
    public string InputHash { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
