using ImageAI.Contracts.Dto;

namespace ImageAI.Ai.Providers;

public interface IImageProvider
{
    Task<GatewayResult> GenerateAsync(GatewayRequest request, CancellationToken ct);
}
