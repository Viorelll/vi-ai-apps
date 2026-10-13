namespace TutorAI.Domain.Entities;

public sealed class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExerciseId { get; set; }
    public Guid RequestId { get; set; }
    public string Answer { get; set; } = "";
    public decimal? Score { get; set; }
    public string? FeedbackJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
