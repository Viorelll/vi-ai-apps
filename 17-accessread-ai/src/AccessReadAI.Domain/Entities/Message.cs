namespace AccessReadAI.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid RequestId { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public List<Citation> Citations { get; set; } = [];
}
