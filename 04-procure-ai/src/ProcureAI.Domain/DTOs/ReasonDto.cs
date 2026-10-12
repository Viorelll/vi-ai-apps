namespace ProcureAI.Domain.DTOs;

public sealed record ReasonDto(string Text, IReadOnlyList<Guid> SourceIds);
