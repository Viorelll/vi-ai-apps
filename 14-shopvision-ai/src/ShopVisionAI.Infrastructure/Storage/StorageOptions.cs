namespace ShopVisionAI.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Endpoint { get; set; } = "http://localhost:19000";
    public string AccessKey { get; set; } = "shopvision-local";
    public string SecretKey { get; set; } = "dummy-local-storage-password";
    public string Bucket { get; set; } = "shopvisionai";
}
