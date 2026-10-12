using System.Text.RegularExpressions;

namespace FeedbackAI.Application.Services;

public static partial class PiiRedactor
{
    public static string Redact(string text)
    {
        var result = Email().Replace(text, "[email]");
        result = Phone().Replace(result, "[phone]");
        return Address().Replace(result, "[address]");
    }

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Email();
    [GeneratedRegex(@"(?<!\w)(?:\+?\d[\d .()/-]{7,}\d)(?!\w)", RegexOptions.CultureInvariant)] private static partial Regex Phone();
    [GeneratedRegex(@"(?i)\b(strada|str\.)\s+[\p{L}0-9 .'-]+\b(?:\s+(?:nr\.?|numărul)\s*\d+)?", RegexOptions.CultureInvariant)] private static partial Regex Address();
}
