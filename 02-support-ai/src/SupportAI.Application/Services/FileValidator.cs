using System.Text;
using SupportAI.Domain.Models;
namespace SupportAI.Application.Services;

public static class FileValidator
{
    public const long MaxBytes = 50L * 1024 * 1024;
    public static string Validate(string fileName, ReadOnlySpan<byte> header)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        bool match;
        string type;
        switch (ext)
        {
            case ".txt":
            case ".md":
            case ".markdown":
                match = !header.Contains((byte)0);
                type = "text/plain";
                break;
            case ".pdf":
                match = header.StartsWith("%PDF-"u8);
                type = "application/pdf";
                break;
            case ".png":
                match = header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                type = "image/png";
                break;
            case ".jpg":
            case ".jpeg":
                match = header.StartsWith(new byte[] { 255, 216, 255 });
                type = "image/jpeg";
                break;
            case ".wav":
                match = header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WAVE"u8);
                type = "audio/wav";
                break;
            case ".mp3":
                match = header.StartsWith("ID3"u8) || (header.Length > 1 && header[0] == 255 && (header[1] & 224) == 224);
                type = "audio/mpeg";
                break;
            case ".mp4":
                match = header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8);
                type = "video/mp4";
                break;
            default:
                throw new SupportException("unsupported_file_type");
        }
        if (!match)
            throw new SupportException("file_signature_mismatch");
        if (ext is ".txt" or ".md" or ".markdown")
        {
            try
            {
                var chars = new char[header.Length];
                new UTF8Encoding(false, true).GetDecoder().Convert(header, chars, flush: false, out _, out _, out _);
            }
            catch (DecoderFallbackException) { throw new SupportException("invalid_text_encoding"); }
        }
        return type;
    }
}
