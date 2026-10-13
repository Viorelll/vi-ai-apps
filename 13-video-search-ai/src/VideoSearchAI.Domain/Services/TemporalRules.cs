using System.Security.Cryptography;
using System.Text;
using VideoSearchAI.Domain.Models;

namespace VideoSearchAI.Domain.Services;

public static class TemporalRules
{
    public static (long Start, long End) ToOriginalRange(
        long start,
        long end,
        long offset,
        long duration
    )
    {
        try
        {
            start = checked(start + offset);
            end = checked(end + offset);
            if (start < 0 || end <= start || end > duration)
                throw new DomainException("invalid_timestamp");
            return (start, end);
        }
        catch (OverflowException)
        {
            throw new DomainException("invalid_timestamp");
        }
    }

    public static Guid SegmentId(Guid videoId, int generation, int ordinal)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{videoId}:{generation}:{ordinal}"))[
            ..16
        ];
        // RFC 9562 UUIDv8 (custom SHA-256), accounting for Guid's little-endian layout.
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}
