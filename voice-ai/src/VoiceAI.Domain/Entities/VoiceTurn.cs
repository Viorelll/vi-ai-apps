namespace VoiceAI.Domain.Entities;

public sealed class VoiceTurn
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public VoiceSession? Session { get; set; }
    public string RequestId { get; set; } = "";
    public string Transcript { get; set; } = "";
    public string Reply { get; set; } = "";
    public string? InputAudioKey { get; set; }
    public string? OutputAudioKey { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
