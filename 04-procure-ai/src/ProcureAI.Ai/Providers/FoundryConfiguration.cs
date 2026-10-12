namespace ProcureAI.Ai.Providers;

public static class FoundryConfiguration
{
    public static bool IsConfigured(IConfiguration configuration)
    {
        var key = configuration["Foundry:ApiKey"];
        return !string.IsNullOrWhiteSpace(key) && !key.StartsWith("replace-", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(configuration["Foundry:OpenAiEndpoint"], UriKind.Absolute, out var endpoint) &&
            endpoint.Scheme == "https" && !endpoint.Host.StartsWith("RESOURCE", StringComparison.OrdinalIgnoreCase);
    }
}
