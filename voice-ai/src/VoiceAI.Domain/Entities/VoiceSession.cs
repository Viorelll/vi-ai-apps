namespace VoiceAI.Domain.Entities;

public sealed class VoiceSession
{
    public Guid Id { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public string State { get; set; } = "Open";
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<VoiceTurn> Turns { get; set; } = [];
    public List<BookingProposal> Proposals { get; set; } = [];
}
