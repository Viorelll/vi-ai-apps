using BuildBudgetAI.Domain.Models;

namespace BuildBudgetAI.Infrastructure;

public static class UploadValidator
{
    public const long MaxBytes = 50 * 1024 * 1024;

    public static string Detect(ReadOnlySpan<byte> bytes, string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext == ".pdf" && bytes.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (ext == ".png" && bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (
            (ext == ".jpg" || ext == ".jpeg")
            && bytes.Length >= 3
            && bytes[0] == 255
            && bytes[1] == 216
            && bytes[2] == 255
        )
            return "image/jpeg";
        if (ext == ".txt" && bytes.Length > 0 && !bytes.Contains((byte)0))
            return "text/plain";
        throw new DomainException("unsupported_file_type");
    }
}
