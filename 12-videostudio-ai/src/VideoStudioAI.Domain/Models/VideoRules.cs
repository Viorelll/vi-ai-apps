namespace VideoStudioAI.Domain.Models;

public static class VideoRules
{
    public const long MaxBytes = 50 * 1024 * 1024;

    public static string MapState(string status, bool verified = false) =>
        status switch
        {
            "queued" => "Queued",
            "in_progress" => "InProgress",
            "completed" => verified ? "Ready" : "Downloading",
            "failed" => "Failed",
            "canceled" => "Canceled",
            _ => "NeedsReview",
        };

    public static TimeSpan PollDelay(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(60, 2 << Math.Clamp(attempt, 0, 5)));

    public static bool IsTerminal(string state) =>
        state is "Ready" or "Failed" or "Canceled" or "Uncertain" or "NeedsReview";

    public static bool ValidProviderId(string id) =>
        id.Length is > 0 and <= 160 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
}
