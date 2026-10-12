namespace VoiceAI.Domain.Entities;

public sealed class AvailabilitySlot
{
    public Guid Id { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public string ResourceLabel { get; set; } = "Consultation";
    public long Revision { get; set; } = 1;
    public LocalBooking? Booking { get; set; }
}
