namespace ImageAI.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "imageai-storage";
    public string SecretKey { get; set; } = "replace-with-storage-password";
    public string Bucket { get; set; } = "imageai";
}
