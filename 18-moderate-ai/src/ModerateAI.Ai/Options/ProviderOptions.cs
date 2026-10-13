namespace ModerateAI.Ai.Options;

public sealed class ProviderOptions
{
    public string Mode { get; set; } = "Demo";
    public string SafetyEndpoint { get; set; } = "https://RESOURCE.cognitiveservices.azure.com/";
    public string SafetyApiKey { get; set; } = "replace-with-safety-key";
    public string OpenAiEndpoint { get; set; } = "https://RESOURCE.openai.azure.com/openai/v1/";
    public string ApiKey { get; set; } = "replace-with-foundry-key";
    public string ChatDeployment { get; set; } = "moderateai-chat";
    public bool PromptShields { get; set; }

    public static bool Valid(ProviderOptions o) =>
        o.Mode == "Demo"
        || (
            o.Mode == "Foundry"
            && ValidEndpoint(o.SafetyEndpoint)
            && ValidEndpoint(o.OpenAiEndpoint)
            && o.OpenAiEndpoint.EndsWith("/openai/v1/", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(o.ChatDeployment)
            && ValidKey(o.SafetyApiKey)
            && ValidKey(o.ApiKey)
        );

    private static bool ValidEndpoint(string s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u)
        && u.Scheme == "https"
        && string.IsNullOrEmpty(u.UserInfo)
        && !u.Host.Contains("RESOURCE", StringComparison.OrdinalIgnoreCase);

    private static bool ValidKey(string s) =>
        !string.IsNullOrWhiteSpace(s)
        && !s.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);
}
