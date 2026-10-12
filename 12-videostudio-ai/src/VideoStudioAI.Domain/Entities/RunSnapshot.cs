namespace VideoStudioAI.Domain.Entities;

public sealed class RunSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string Deployment { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public string PromptVersion { get; set; } = "storyboard-v1";
    public string PipelineVersion { get; set; } = "video-v1";
}
