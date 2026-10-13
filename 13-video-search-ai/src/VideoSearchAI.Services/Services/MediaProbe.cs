using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Services.Options;

namespace VideoSearchAI.Services.Services;

public sealed class MediaProbe(IOptions<PipelineOptions> options)
{
    public async Task<long> DurationAsync(string path, CancellationToken ct)
    {
        var info = new ProcessStartInfo(options.Value.FfprobePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (
            var arg in new[]
            {
                "-v",
                "error",
                "-select_streams",
                "v:0",
                "-show_entries",
                "stream=codec_type:format=duration",
                "-of",
                "json",
                path,
            }
        )
            info.ArgumentList.Add(arg);
        using var process =
            Process.Start(info) ?? throw new DomainException("media_probe_unavailable", 503);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(true);
            throw;
        }
        var output = await outputTask;
        await errorTask;
        if (process.ExitCode != 0)
            throw new DomainException("invalid_video");
        using var json = System.Text.Json.JsonDocument.Parse(output);
        if (
            !json.RootElement.TryGetProperty("streams", out var streams)
            || streams.GetArrayLength() == 0
        )
            throw new DomainException("video_stream_required");
        if (
            !json.RootElement.GetProperty("format").TryGetProperty("duration", out var value)
            || !double.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var seconds)
            || !double.IsFinite(seconds)
            || seconds <= 0
            || seconds * 1000 > options.Value.MaxDurationMs
        )
            throw new DomainException("invalid_video_duration");
        return checked((long)Math.Round(seconds * 1000));
    }
}
