using System.Text.RegularExpressions;

namespace IncidentAI.Application.Services;

public static partial class Redactor
{
    [GeneratedRegex(@"(?i)(password|passwd|secret|api[_-]?key|token|authorization)\s*[:=]\s*[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignment();
    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Email();
    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex IpAddress();

    public static string Redact(string value) => IpAddress().Replace(Email().Replace(SecretAssignment().Replace(value, "$1=[REDACTED]"), "[EMAIL]"), "[IP]");
}
