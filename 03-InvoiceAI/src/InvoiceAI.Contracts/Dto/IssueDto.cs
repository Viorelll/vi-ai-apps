namespace InvoiceAI.Contracts.Dto;

public sealed record IssueDto(string Code, string Severity, string Path, string DetailsJson);
