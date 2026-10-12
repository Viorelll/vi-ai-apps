namespace ImageAI.Domain.Entities;

public sealed class ImageProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Brief { get; set; } = "";
    public string State { get; set; } = "Created";
    public int Revision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
