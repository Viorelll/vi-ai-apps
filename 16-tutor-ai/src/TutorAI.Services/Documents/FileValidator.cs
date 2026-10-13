using System.IO.Compression;
using System.Text;
using TutorAI.Domain.Models;

namespace TutorAI.Services.Documents;

public static class FileValidator
{
    public const long MaxBytes = 50L * 1024 * 1024;

    public static async Task<string> ValidateAsync(
        string fileName,
        Stream stream,
        CancellationToken ct
    )
    {
        if (stream.Length is <= 0 or > MaxBytes)
            throw new AppException(413, "file_size_limit", "File must contain 1 byte to 50 MiB.");
        byte[] header = new byte[12];
        var n = await stream.ReadAsync(header, ct);
        stream.Position = 0;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        bool starts(byte[] bytes) =>
            n >= bytes.Length && header.AsSpan(0, bytes.Length).SequenceEqual(bytes);
        string? type = ext switch
        {
            ".pdf" when starts(Encoding.ASCII.GetBytes("%PDF-")) => "application/pdf",
            ".png" when starts([137, 80, 78, 71, 13, 10, 26, 10]) => "image/png",
            ".jpg" or ".jpeg" when starts([255, 216, 255]) => "image/jpeg",
            ".docx" when starts([80, 75, 3, 4]) =>
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" or ".md" => "text/plain",
            _ => null,
        };
        if (type is null)
            throw new AppException(
                415,
                "unsupported_file",
                "Accept PDF, DOCX, PNG, JPEG or UTF-8 text with matching file signatures."
            );
        if (type == "text/plain")
        {
            if (stream.Length > 2 * 1024 * 1024)
                throw new AppException(413, "text_limit", "Text files are limited to 2 MiB.");
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(false, true),
                false,
                4096,
                true
            );
            try
            {
                var text = await reader.ReadToEndAsync(ct);
                if (text.Contains('\0') || string.IsNullOrWhiteSpace(text))
                    throw new AppException(
                        415,
                        "invalid_text",
                        "Text file contains no readable text."
                    );
            }
            catch (DecoderFallbackException)
            {
                throw new AppException(415, "invalid_text", "Text must be valid UTF-8.");
            }
        }
        if (ext == ".docx")
        {
            try
            {
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
                if (
                    zip.GetEntry("word/document.xml") is null
                    || zip.Entries.Sum(x => x.Length) > 50L * 1024 * 1024
                )
                    throw new AppException(
                        415,
                        "invalid_docx",
                        "DOCX structure is invalid or too large when expanded."
                    );
            }
            catch (InvalidDataException)
            {
                throw new AppException(415, "invalid_docx", "Invalid DOCX archive.");
            }
        }
        stream.Position = 0;
        return type;
    }
}
