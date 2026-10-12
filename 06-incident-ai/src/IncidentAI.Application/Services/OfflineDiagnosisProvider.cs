using System.Diagnostics;
using IncidentAI.Application.Contracts;

namespace IncidentAI.Application.Services;

public sealed class OfflineDiagnosisProvider : IDiagnosisProvider
{
    public Task<DiagnosisResult> DiagnoseAsync(DiagnosisInput input, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var errors = input.Events.Where(x => x.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)
            || x.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.TimestampUtc).Take(100).ToArray();
        var correlated = errors.Where(x => !string.IsNullOrWhiteSpace(x.TraceId)).GroupBy(x => x.TraceId)
            .OrderByDescending(g => g.Count()).FirstOrDefault();
        var evidence = correlated?.Select(x => x.SourceId).Distinct().Take(20).ToArray() ?? errors.Select(x => x.SourceId).Take(20).ToArray();
        var hypotheses = evidence.Length == 0 ? Array.Empty<HypothesisResult>() : [new HypothesisResult(
            correlated is null ? "Erori observate în fereastra incidentului; cauza nu este confirmată." : $"Erorile se grupează pe trace {correlated.Key}; verifică timpul de răspuns al dependențelor asociate.",
            "hypothesis", evidence, [])];
        var latestRelease = input.Releases.Where(x => x.DeployedAtUtc.HasValue).OrderByDescending(x => x.DeployedAtUtc).FirstOrDefault();
        var checks = evidence.Length == 0 ? new[] { "Importă loguri pentru intervalul incidentului și rulează analiza din nou." }
            : new[] { "Compară latența dependenței și rata timeout-urilor pentru trace-urile citate.", "Verifică loguri din jurul evenimentelor; această analiză nu demonstrează cauzalitatea." };
        if (latestRelease is not null) checks = checks.Append($"Compară cronologia cu release-ul {latestRelease.Label}; proximitatea temporală nu dovedește cauza.").ToArray();
        watch.Stop();
        return Task.FromResult(new DiagnosisResult("completed", evidence.Length == 0 ? "Nu au fost identificate erori în sursele disponibile." : $"Au fost analizate {input.Events.Count} evenimente redacted și identificate {evidence.Length} surse relevante.", hypotheses, checks, "offline", 0, 0, watch.ElapsedMilliseconds));
    }
}
