namespace VideoSearchAI.Ai.Options;

public sealed class AiOptions
{
    public string Mode { get; set; } = "Demo";
    public string InternalKey { get; set; } = "dummy-internal-key-change-me";
    public string DemoSeedHash { get; set; } = "";
    public string OpenAiEndpoint { get; set; } = "https://DUMMY.openai.azure.com/openai/v1/";
    public string ApiKey { get; set; } = "DUMMY";
    public string ChatDeployment { get; set; } = "gpt-4.1-mini";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 1536;
    public string ContentEndpoint { get; set; } = "https://DUMMY.cognitiveservices.azure.com/";
    public string ContentKey { get; set; } = "DUMMY";
    public string AnalyzerId { get; set; } = "prebuilt-videoSearch";
    public string SearchEndpoint { get; set; } = "https://DUMMY.search.windows.net/";
    public string SearchKey { get; set; } = "DUMMY";
    public string SearchIndex { get; set; } = "videosearchai-v1";
    public int MaxToolRounds { get; set; } = 5;
    public long MaxRunTokens { get; set; } = 15000;
}
