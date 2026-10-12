namespace VoiceAI.Domain.DTOs;

public sealed record AiTurnRequest(
    string Text,
    string TimeZoneId,
    IReadOnlyList<AvailableSlotDto> AvailableSlots
);
