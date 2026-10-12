namespace MeetingAI.Api.Services;

public static class AudioFileValidator
{
    public static string? DetectContentType(string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".wav" when header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WAVE"u8) => "audio/wav",
            ".mp3" when header.StartsWith("ID3"u8) || header.Length >= 2 && header[0] == 0xff && (header[1] & 0xe0) == 0xe0 => "audio/mpeg",
            ".ogg" when header.StartsWith("OggS"u8) => "audio/ogg",
            ".m4a" or ".mp4" when header.Length >= 8 && header.Slice(4, 4).SequenceEqual("ftyp"u8) => "audio/mp4",
            ".webm" when header.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }) => "audio/webm",
            _ => null
        };
    }
}
