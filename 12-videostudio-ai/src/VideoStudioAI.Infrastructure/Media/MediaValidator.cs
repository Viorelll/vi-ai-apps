using System.Diagnostics;
using System.Text.Json;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Infrastructure.Media;

public sealed class MediaValidator : IMediaValidator
{
    public async Task ValidateAsync(
        string path,
        int seconds,
        int width,
        int height,
        CancellationToken ct
    )
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("ffprobe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (
            var arg in new[]
            {
                "-v",
                "error",
                "-show_entries",
                "format=format_name,duration:stream=codec_type,width,height",
                "-of",
                "json",
                path,
            }
        )
            process.StartInfo.ArgumentList.Add(arg);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            process.Kill(true);
            throw;
        }
        var json = await output;
        await error;
        if (process.ExitCode != 0)
            throw new AppException(422, "invalid_media", "Content is not a valid media file.");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var valid = root.GetProperty("format")
            .GetProperty("format_name")
            .GetString()!
            .Split(',')
            .Contains("mp4");
        var duration = double.Parse(
            root.GetProperty("format").GetProperty("duration").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture
        );
        var video = root.GetProperty("streams")
            .EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("codec_type").GetString() == "video");
        if (
            !valid
            || video.ValueKind == JsonValueKind.Undefined
            || video.GetProperty("width").GetInt32() != width
            || video.GetProperty("height").GetInt32() != height
            || Math.Abs(duration - seconds) > 0.6
        )
            throw new AppException(
                422,
                "media_mismatch",
                "MP4 duration or dimensions do not match the requested clip."
            );
    }
}
