using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModerateAI.Domain.Dto;

namespace ModerateAI.Domain.Services;

public static class PolicyEvaluator
{
    public const string Version = "policy-four-v1";
    public static readonly string[] Categories = ["Hate", "SelfHarm", "Sexual", "Violence"];

    public static PolicyResult Evaluate(SafetySnapshot snapshot)
    {
        var reasons = new List<string>();
        bool valid =
            snapshot.Complete
            && snapshot.OutputScale == "FourSeverityLevels"
            && snapshot.Scores.Count == 4
            && Categories.All(c => snapshot.Scores.Count(x => x.Category == c) == 1)
            && snapshot.Scores.All(x => new[] { 0, 2, 4, 6 }.Contains(x.Severity));
        string decision;
        if (!valid)
        {
            decision = "review";
            reasons.Add("Incomplete or unsupported provider result; manual review required.");
        }
        else
        {
            int maximum = snapshot.Scores.Max(x => x.Severity);
            decision =
                maximum >= 4 ? "block"
                : maximum == 2 ? "review"
                : "allow";
            reasons.AddRange(
                snapshot
                    .Scores.Where(x => x.Severity > 0)
                    .Select(x => $"{x.Category}: severity {x.Severity} on FourSeverityLevels.")
            );
            if (reasons.Count == 0)
                reasons.Add("All four categories returned severity 0.");
        }
        var hash = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            Version,
                            snapshot.ApiVersion,
                            snapshot.OutputScale,
                            snapshot.Complete,
                            Scores = snapshot.Scores.OrderBy(x => x.Category),
                            decision,
                        }
                    )
                )
            )
        );
        return new(decision, Version, reasons, hash);
    }
}
