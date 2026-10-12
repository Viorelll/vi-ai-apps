namespace ProcureAI.Domain.DTOs;

public sealed record SourceDto(Guid DocumentId, Guid SourceId, string FileName, int Generation, string Text, int? PageNumber);
