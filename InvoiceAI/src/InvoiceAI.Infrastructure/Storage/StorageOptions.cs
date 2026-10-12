namespace InvoiceAI.Infrastructure.Storage;

public sealed class StorageOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "invoiceai-storage";
    public string SecretKey { get; set; } = "dummy-storage-password-change-me";
    public string Bucket { get; set; } = "invoiceai";
}
