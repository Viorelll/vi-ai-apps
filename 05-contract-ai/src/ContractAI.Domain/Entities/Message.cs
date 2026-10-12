namespace ContractAI.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VersionId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid RequestId { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Deployment { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
