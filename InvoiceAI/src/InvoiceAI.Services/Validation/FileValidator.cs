using InvoiceAI.Services.Exceptions;
namespace InvoiceAI.Services.Validation;

public static class FileValidator
{
    public const long MaxBytes = 50L * 1024 * 1024;
    public static string Detect(ReadOnlySpan<byte> bytes, string fileName, string claimedType)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        string? actual = null;
        if (bytes.StartsWith("%PDF-"u8) && ext == ".pdf")
            actual = "application/pdf";
        else if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && ext == ".png")
            actual = "image/png";
        else if (bytes.StartsWith(new byte[] { 255, 216, 255 }) && ext is ".jpg" or ".jpeg")
            actual = "image/jpeg";
        else if ((bytes.StartsWith("II*\0"u8) || bytes.StartsWith("MM\0*"u8)) && ext is ".tif" or ".tiff")
            actual = "image/tiff";
        if (actual is null || actual != claimedType)
            throw new AppException("FILE_TYPE_INVALID", 415);
        return actual;
    }
}
