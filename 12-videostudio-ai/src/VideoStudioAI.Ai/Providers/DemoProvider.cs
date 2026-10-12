using System.Diagnostics;
using VideoStudioAI.Ai.Interfaces;
using VideoStudioAI.Ai.Models;
using VideoStudioAI.Contracts.Dto;

namespace VideoStudioAI.Ai.Providers;

// Deliberate offline fixture. It never claims to perform Foundry inference.
public sealed class DemoProvider : IProvider
{
    public Task<Storyboard> StoryboardAsync(string brief, CancellationToken ct) =>
        Task.FromResult(
            new Storyboard(
                "Scenariu demonstrativ",
                [
                    new Scene(
                        "scene-1",
                        "Cadru principal",
                        $"A cinematic composition inspired by: {brief}. Slow camera movement, natural light, clear focal subject."
                    ),
                ],
                0,
                0
            )
        );

    public Task<ProviderJob> SubmitAsync(VideoSubmit input, CancellationToken ct)
    {
        var size = input.Size.Split('x');
        return Task.FromResult(
            new ProviderJob(
                $"demo_{input.Seconds}_{size[0]}_{size[1]}_{Guid.NewGuid():N}",
                "queued"
            )
        );
    }

    public Task<ProviderJob> PollAsync(string id, CancellationToken ct)
    {
        Parse(id);
        return Task.FromResult(new ProviderJob(id, "completed"));
    }

    public async Task<string> DownloadAsync(string id, CancellationToken ct)
    {
        var (seconds, width, height) = Parse(id);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        var color = id[^1] % 2 == 0 ? "0x164e63" : "0x6d28d9";
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo("ffmpeg")
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
                "-f",
                "lavfi",
                "-i",
                $"color=c={color}:s={width}x{height}:r=24",
                "-vf",
                "drawtext=text='VideoStudioAI DEMO':fontcolor=white:fontsize=48:x=(w-tw)/2:y=(h-th)/2",
                "-t",
                seconds.ToString(),
                "-c:v",
                "libx264",
                "-threads",
                "2",
                "-pix_fmt",
                "yuv420p",
                "-movflags",
                "+faststart",
                "-y",
                path,
            }
        )
            p.StartInfo.ArgumentList.Add(arg);
        try
        {
            p.Start();
            var err = p.StandardError.ReadToEndAsync(ct);
            var output = p.StandardOutput.ReadToEndAsync(ct);
            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch
            {
                p.Kill(true);
                throw;
            }
            await output;
            await err;
            if (p.ExitCode != 0)
                throw new GatewayException(
                    500,
                    "demo_media_failed",
                    "FFmpeg could not generate the demo fixture."
                );
            return path;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    private static (int, int, int) Parse(string id)
    {
        FoundryProvider.ValidateId(id);
        var parts = id.Split('_');
        if (
            parts.Length != 5
            || parts[0] != "demo"
            || !int.TryParse(parts[1], out var seconds)
            || !int.TryParse(parts[2], out var width)
            || !int.TryParse(parts[3], out var height)
            || seconds is not (4 or 8 or 12)
            || !((width == 1280 && height == 720) || (width == 720 && height == 1280))
        )
            throw new GatewayException(400, "demo_id_invalid", "Invalid demo video ID.");
        return (seconds, width, height);
    }

    public Task<GroundedAnswer> AnswerAsync(GroundedRequest input, CancellationToken ct)
    {
        var sources = input.Sources.Take(3).ToArray();
        return Task.FromResult(
            new GroundedAnswer(
                sources.Length == 0
                    ? "Nu există surse suficiente."
                    : "Răspuns demonstrativ din sursele selectate:\n"
                        + string.Join("\n", sources.Select(s => s.Excerpt)),
                sources.Select(s => s.ChunkId).ToArray()
            )
        );
    }

    public Task<string> DescribeImageAsync(
        string contentType,
        string base64,
        CancellationToken ct
    ) =>
        throw new GatewayException(
            409,
            "vision_requires_foundry",
            "Image understanding requires Foundry mode and a configured vision deployment."
        );
}
