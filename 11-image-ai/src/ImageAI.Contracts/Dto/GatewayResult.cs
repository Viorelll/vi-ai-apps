namespace ImageAI.Contracts.Dto;

public sealed record GatewayResult(
    string ImageBase64,
    string Deployment,
    string? UsageJson,
    string? ProviderRequestId,
    string SafetyStatus,
    string? PreparedPrompt = null
);
