namespace InspectAI.Domain.DTOs;

public sealed record CreateInspectionRequest(
    string EquipmentLabel,
    string EquipmentModel,
    string ManualVersion
);
