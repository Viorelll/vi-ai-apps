namespace ShopVisionAI.Ai.Providers;

public sealed class FoundryOptions
{
    public string Mode { get; set; } = "Foundry";
    public string Endpoint { get; set; } = "https://RESOURCE.openai.azure.com/openai/v1/";
    public string ApiKey { get; set; } = "dummy-replace-with-resource-key";
    public string Deployment { get; set; } = "dummy-vision-deployment";
}
