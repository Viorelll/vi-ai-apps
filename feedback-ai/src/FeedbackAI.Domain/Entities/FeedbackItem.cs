namespace FeedbackAI.Domain.Entities;

public sealed class FeedbackItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public FeedbackBatch Batch { get; set; } = null!;
    public int Ordinal { get; set; }
    public string OriginalText { get; set; } = "";
    public string RedactedText { get; set; } = "";
    public string? Language { get; set; }
    public Classification? Classification { get; set; }
    public string ActiveTopic { get; set; } = "unknown";
    public string ActiveUrgency { get; set; } = "unknown";
}
