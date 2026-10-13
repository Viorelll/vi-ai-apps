using System.Text;
using AccessReadAI.Contracts;

namespace AccessReadAI.Services.Validation;

public static class FileValidator
{
    public const long MaxBytes = 50 * 1024 * 1024;

    public static string Detect(string fileName, ReadOnlySpan<byte> prefix)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".pdf" && prefix.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (ext == ".png" && prefix.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (ext is ".jpg" or ".jpeg" && prefix.StartsWith(new byte[] { 255, 216, 255 }))
            return "image/jpeg";
        if (ext is ".txt" or ".md")
        {
            try
            {
                var decoder = new UTF8Encoding(false, true).GetDecoder();
                var output = new char[prefix.Length];
                decoder.Convert(prefix, output, false, out _, out _, out _);
                if (prefix.Contains((byte)0))
                    throw new ApiFault(400, "invalid_file");
                return "text/plain";
            }
            catch (DecoderFallbackException)
            {
                throw new ApiFault(400, "invalid_utf8");
            }
        }
        throw new ApiFault(415, "unsupported_or_mismatched_file");
    }
}
