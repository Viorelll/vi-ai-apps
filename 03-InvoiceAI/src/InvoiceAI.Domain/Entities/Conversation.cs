namespace InvoiceAI.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId
    {
        get; set;
    }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
