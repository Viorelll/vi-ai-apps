namespace TutorAI.Domain.Entities;

public sealed class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Kind { get; set; } = "single_choice";
    public string Prompt { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public string RubricJson { get; set; } = "{}";
    public string SourceIdsJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "[]";
    public string Version { get; set; } = "rubric-v1";
    public int Difficulty { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
