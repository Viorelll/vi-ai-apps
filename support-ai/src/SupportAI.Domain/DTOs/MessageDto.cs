namespace SupportAI.Domain.DTOs;

public sealed record MessageDto(Guid Id, string Role, string Text, bool Stale, long InputTokens, long OutputTokens, long DurationMs, IReadOnlyList<CitationDto> Citations);
