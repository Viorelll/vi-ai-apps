namespace VoiceAI.Domain.DTOs;

public sealed record AvailableSlotDto(
    Guid Id,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string ResourceLabel
);
