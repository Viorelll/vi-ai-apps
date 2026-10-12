namespace ImageAI.Ai.Options;

public sealed class ImagesOptions
{
    public string Mode { get; set; } = "Foundry";
    public string Endpoint { get; set; } = "https://RESOURCE.openai.azure.com/";
    public string ApiKey { get; set; } = "replace-with-foundry-key";
    public string Deployment { get; set; } = "replace-image-deployment";
    public string ApiVersion { get; set; } = "2025-04-01-preview";
    public string ChatDeployment { get; set; } = "";
    public string InternalApiKey { get; set; } = "replace-with-internal-secret";
}
