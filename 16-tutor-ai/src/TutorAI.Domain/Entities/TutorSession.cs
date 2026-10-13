namespace TutorAI.Domain.Entities;

public sealed class TutorSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Topic { get; set; } = "";
    public int Difficulty { get; set; } = 1;
    public long Revision { get; set; }
    public string State { get; set; } = "Open";
    public string? ExplanationJson { get; set; }
    public Guid? CurrentExerciseId { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
