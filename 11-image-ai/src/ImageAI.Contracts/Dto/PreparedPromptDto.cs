namespace ImageAI.Contracts.Dto;

public sealed record PreparedPromptDto(string Prompt, string? UsageJson, string? Deployment);
