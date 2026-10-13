namespace EvalAI.Domain.DTOs;

public sealed record VariantDefinition(Guid Id, string Name, string DeploymentAlias, string PromptVersion = "candidate-v1", string ToolsetVersion = "none-v1", string Mode = "Model");
