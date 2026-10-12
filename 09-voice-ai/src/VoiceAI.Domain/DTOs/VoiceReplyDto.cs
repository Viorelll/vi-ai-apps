namespace VoiceAI.Domain.DTOs;

public sealed record VoiceReplyDto(
    string Text,
    string Transcript,
    Guid? ProposalId,
    string? AudioBase64,
    string? AudioContentType
);
