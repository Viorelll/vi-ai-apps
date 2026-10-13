namespace BuildBudgetAI.Infrastructure;

public sealed class StorageOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "dummy-storage-user";
    public string SecretKey { get; set; } = "dummy-storage-password";
    public string Bucket { get; set; } = "buildbudgetai";
}
