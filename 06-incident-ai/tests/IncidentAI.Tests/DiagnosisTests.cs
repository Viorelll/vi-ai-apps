using IncidentAI.Application.Contracts;
using IncidentAI.Application.Services;

namespace IncidentAI.Tests;

public sealed class DiagnosisTests
{
    [Fact]
    public void Redactor_masks_credentials_and_personal_identifiers()
    {
        var result = Redactor.Redact("api_key=super-secret email=person@example.com host=10.20.30.40");
        Assert.DoesNotContain("super-secret", result);
        Assert.DoesNotContain("person@example.com", result);
        Assert.DoesNotContain("10.20.30.40", result);
        Assert.Contains("[REDACTED]", result);
        Assert.Contains("[EMAIL]", result);
        Assert.Contains("[IP]", result);
    }

    [Fact]
    public async Task Offline_diagnosis_cites_only_matching_error_trace_and_never_claims_cause()
    {
        var from = DateTimeOffset.Parse("2026-10-11T10:00:00Z");
        var input = new DiagnosisInput(Guid.NewGuid(), "checkout", from, from.AddHours(1),
        [
            new LogPoint("E1", from.AddMinutes(2), "Error", "T1", "timeout"),
            new LogPoint("E2", from.AddMinutes(3), "Information", "T2", "healthy"),
            new LogPoint("E3", from.AddMinutes(4), "Error", "T1", "timeout")
        ], [new ReleasePoint("R2", from)]);
        var result = await new OfflineDiagnosisProvider().DiagnoseAsync(input, CancellationToken.None);
        var hypothesis = Assert.Single(result.Hypotheses);
        Assert.Equal(new[] { "E1", "E3" }, hypothesis.EvidenceIds);
        Assert.Equal("hypothesis", hypothesis.ConfidenceLabel);
        Assert.Contains(result.NextChecks, check => check.Contains("nu dovedește cauza", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Offline_diagnosis_returns_no_hypothesis_when_no_errors_exist()
    {
        var from = DateTimeOffset.Parse("2026-10-11T10:00:00Z");
        var input = new DiagnosisInput(Guid.NewGuid(), "catalog", from, from.AddHours(1),
            [new LogPoint("E1", from, "Information", null, "ok")], []);
        var result = await new OfflineDiagnosisProvider().DiagnoseAsync(input, CancellationToken.None);
        Assert.Empty(result.Hypotheses);
        Assert.Contains("Nu au fost identificate erori", result.Summary);
    }
}
