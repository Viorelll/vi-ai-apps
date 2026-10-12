namespace FeedbackAI.Domain.Entities;

public sealed class Classification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public FeedbackItem Item { get; set; } = null!;
    public string Topic { get; set; } = "unknown";
    public string Sentiment { get; set; } = "neutral";
    public string Urgency { get; set; } = "unknown";
    public string Reason { get; set; } = "";
    public string Provider { get; set; } = "Demo";
    public string ModelVersion { get; set; } = "rules-v1";
    public string PromptVersion { get; set; } = "feedback-v1";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
