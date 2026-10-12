namespace ShopVisionAI.Domain.Entities;

public sealed class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SearchId { get; set; }
    public VisualSearch Search { get; set; } = null!;
    public Guid RequestId { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
