namespace TutorAI.Domain.Entities;

public sealed class LearningSource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DocumentId { get; set; }
    public string OfficialUrl { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTimeOffset CheckedAtUtc { get; set; }
    public bool Active { get; set; } = true;
}
