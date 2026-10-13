namespace EvalAI.Domain.DTOs;

public sealed record CaseInput(string Name, System.Text.Json.JsonElement Input, System.Text.Json.JsonElement Expected, Guid[]? EvidenceIds = null);
