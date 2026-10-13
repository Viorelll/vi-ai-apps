namespace BuildBudgetAI.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "assistant";
    public string Text { get; set; } = "";
    public string Deployment { get; set; } = "";
    public string PromptVersion { get; set; } = "prompt-v1";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
