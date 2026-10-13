namespace VideoSearchAI.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "demo-minio";
    public string SecretKey { get; set; } = "demo-minio-password";
    public string Bucket { get; set; } = "videosearchai";
}
