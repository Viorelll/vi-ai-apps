namespace EvalAI.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid RequestId { get; set; }
    public string Question { get; set; } = ""; public string AnswerJson { get; set; } = "{}"; public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
