namespace TutorAI.Domain.Rules;

public static class OfficialSourcePolicy
{
    public static bool IsAllowed(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https"
        && uri.Host == "learn.microsoft.com"
        && uri.Port == 443
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Fragment);
}
