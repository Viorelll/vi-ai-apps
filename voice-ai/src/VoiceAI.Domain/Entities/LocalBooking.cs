namespace VoiceAI.Domain.Entities;

public sealed class LocalBooking
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public BookingProposal? Proposal { get; set; }
    public Guid SlotId { get; set; }
    public AvailabilitySlot? Slot { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public DateTimeOffset ConfirmedAtUtc { get; set; }
}
