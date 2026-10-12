namespace InvoiceAI.Contracts.Dto;

public sealed record FieldDto(string Path, string RawText, System.Text.Json.JsonElement Value, decimal? Confidence, IReadOnlyList<EvidenceDto> Evidence, System.Text.Json.JsonElement? CorrectedValue = null);
