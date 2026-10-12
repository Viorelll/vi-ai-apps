namespace VoiceAI.Domain.DTOs;

public sealed record AiTurnResult(string Reply, Guid? SlotId);
