namespace InspectAI.Domain.DTOs;

public sealed record AiRequest(
    string Step,
    Guid OperationId,
    string InputHash,
    string PromptVersion,
    PhotoSource[] Photos,
    ManualSource[] Sources,
    string EquipmentModel,
    string ManualVersion,
    string? Question = null
);
