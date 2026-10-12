namespace VoiceAI.Domain.Entities;

public sealed class BookingProposal
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public VoiceSession? Session { get; set; }
    public Guid SlotId { get; set; }
    public AvailabilitySlot? Slot { get; set; }
    public Guid? VoiceTurnId { get; set; }
    public VoiceTurn? VoiceTurn { get; set; }
    public long SlotRevision { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string State { get; set; } = "Draft";
    public long Revision { get; set; } = 1;
    public LocalBooking? Booking { get; set; }
}
