using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Services;

namespace ModerateAI.Tests;

public sealed class TestAnalysisGateway : IAnalysisGateway
{
    public int Calls { get; private set; }
    public Exception? Failure { get; set; }

    public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken ct)
    {
        Calls++;
        if (Failure != null)
            throw Failure;
        int value =
            request.Text == "review" ? 2
            : request.Text == "block" ? 4
            : 0;
        var snapshot = new SafetySnapshot(
            "IntegrationFake",
            "2024-09-01",
            "FourSeverityLevels",
            PolicyEvaluator
                .Categories.Select(c => new SafetyScore(c, c == "Violence" ? value : 0))
                .ToArray(),
            true,
            "{\"integration\":true}"
        );
        return Task.FromResult(
            new AnalysisResult(snapshot, "Synthetic integration explanation", 2, 1)
        );
    }
}
