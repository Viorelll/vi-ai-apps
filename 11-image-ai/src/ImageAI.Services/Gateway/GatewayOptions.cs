namespace ImageAI.Services.Gateway;

public sealed class GatewayOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5081/";
    public string InternalApiKey { get; set; } = "replace-with-internal-secret";
}
