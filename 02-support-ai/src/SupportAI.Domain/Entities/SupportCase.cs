namespace SupportAI.Domain.Entities;

public sealed class SupportCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? OrderReference
    {
        get; set;
    }
    public string State { get; set; } = "Open";
    public long Revision
    {
        get; set;
    }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
