namespace ShopVisionAI.Contracts.Dto;

public sealed record QuestionResponse(string Answer, IReadOnlyList<string> SourceIds);
