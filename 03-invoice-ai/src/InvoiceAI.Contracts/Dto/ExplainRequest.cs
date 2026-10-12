namespace InvoiceAI.Contracts.Dto;

public sealed record ExplainRequest(Guid InvoiceId, IReadOnlyList<FieldDto> Fields, IReadOnlyList<LineDto> Lines, IReadOnlyList<IssueDto> Issues, string? Question = null, IReadOnlyList<ChatTurnDto>? History = null);
