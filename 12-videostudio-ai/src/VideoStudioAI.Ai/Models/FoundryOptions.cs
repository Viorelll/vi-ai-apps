namespace VideoStudioAI.Ai.Models;

public sealed class FoundryOptions
{
    public string Mode { get; set; } = "Foundry";
    public string OpenAiEndpoint { get; set; } = "https://RESOURCE.openai.azure.com/openai/v1/";
    public string ApiKey { get; set; } = "dummy-foundry-api-key";
    public string ChatDeployment { get; set; } = "dummy-gpt-4.1-mini";
    public string VideoDeployment { get; set; } = "dummy-sora-2";
    public int[] Seconds { get; set; } = [4, 8, 12];
    public string[] Sizes { get; set; } = ["1280x720", "720x1280"];
    public bool VideoEnabled { get; set; } = true;
}
