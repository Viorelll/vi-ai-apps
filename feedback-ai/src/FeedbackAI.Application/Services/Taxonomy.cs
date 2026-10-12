namespace FeedbackAI.Application.Services;

public static class Taxonomy
{
    public static readonly string[] Topics = ["delivery", "product", "information", "other", "unknown"];
    public static readonly string[] Urgencies = ["normal", "high", "unknown"];
    public static bool IsValid(string field, string value) => field switch
    {
        "topic" => Topics.Contains(value, StringComparer.OrdinalIgnoreCase),
        "urgency" => Urgencies.Contains(value, StringComparer.OrdinalIgnoreCase),
        _ => false
    };
}
