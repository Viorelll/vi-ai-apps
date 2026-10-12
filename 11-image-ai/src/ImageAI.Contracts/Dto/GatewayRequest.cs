namespace ImageAI.Contracts.Dto;

public sealed record GatewayRequest(
    Guid OperationId,
    string Prompt,
    string Size,
    string Quality,
    string? ImageBase64,
    string? MaskBase64
);
