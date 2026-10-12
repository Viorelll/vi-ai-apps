namespace InspectAI.Domain.Entities;

public sealed class QuestionMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionId { get; set; }
    public Guid RequestId { get; set; }
    public string Question { get; set; } = "";
    public string AnswerJson { get; set; } = "{}";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
