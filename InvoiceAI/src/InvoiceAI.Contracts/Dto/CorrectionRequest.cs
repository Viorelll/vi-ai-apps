namespace InvoiceAI.Contracts.Dto;

public sealed record CorrectionRequest(long Revision, Dictionary<string, System.Text.Json.JsonElement> Corrections);
