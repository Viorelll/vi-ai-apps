namespace InvoiceAI.Contracts.Dto;

public sealed record ExplanationDto(string Summary, string? SuggestedCategory, IReadOnlyList<string> SourceIds, string Deployment, long InputTokens, long OutputTokens);
