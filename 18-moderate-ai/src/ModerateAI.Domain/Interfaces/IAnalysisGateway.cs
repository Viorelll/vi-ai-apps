using ModerateAI.Domain.Dto;

namespace ModerateAI.Domain.Interfaces;

public interface IAnalysisGateway
{
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken ct);
}
