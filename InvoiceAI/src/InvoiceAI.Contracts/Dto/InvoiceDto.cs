namespace InvoiceAI.Contracts.Dto;

public sealed record InvoiceDto(Guid Id, Guid DocumentId, string Kind, string State, long Revision, string? VendorName, string? InvoiceNumber, DateOnly? Date, string? Currency, decimal? Subtotal, decimal? Tax, decimal? Total, string? Explanation, string? SuggestedCategory, string? ErrorCode, long InputTokens, long OutputTokens, IReadOnlyList<LineDto> Lines, IReadOnlyList<FieldDto> Fields, IReadOnlyDictionary<string, System.Text.Json.JsonElement> Corrections, IReadOnlyList<IssueDto> Issues);
