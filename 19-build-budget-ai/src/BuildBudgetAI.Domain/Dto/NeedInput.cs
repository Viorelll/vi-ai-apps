namespace BuildBudgetAI.Domain.Dto;

public sealed record NeedInput(string Label, string Unit, decimal Quantity, decimal WastePercent);
