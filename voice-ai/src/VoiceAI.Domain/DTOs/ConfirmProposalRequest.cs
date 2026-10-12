namespace VoiceAI.Domain.DTOs;

public sealed record ConfirmProposalRequest(long Revision, string IdempotencyKey);
