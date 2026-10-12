namespace VideoSearchAI.Services.Options;

public sealed class PipelineOptions
{
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;
    public long MaxDurationMs { get; set; } = 1800000;
    public int MaxRunTokens { get; set; } = 15000;
    public int MaxAttempts { get; set; } = 3;
    public string FfprobePath { get; set; } = "ffprobe";
}
