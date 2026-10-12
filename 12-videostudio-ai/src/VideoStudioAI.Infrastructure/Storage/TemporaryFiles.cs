using System.Security.Cryptography;
using VideoStudioAI.Domain.Models;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Infrastructure.Storage;

public static class TemporaryFiles
{
    public static async Task<StoredObject> SpoolAsync(
        Stream input,
        string contentType,
        CancellationToken ct
    )
    {
        var path = Path.GetTempFileName();
        try
        {
            await using var output = File.Create(path);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                if (size > VideoRules.MaxBytes)
                    throw new AppException(
                        413,
                        "content_too_large",
                        "Provider content exceeds 50 MiB."
                    );
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (size == 0)
                throw new AppException(502, "content_empty", "Provider content is empty.");
            return new(path, Convert.ToHexStringLower(hash.GetHashAndReset()), size, contentType);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }
}
