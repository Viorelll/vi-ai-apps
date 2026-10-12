using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Database;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Domain.Models;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Services;

public sealed class ProjectService(AppDbContext db, IAiGateway ai)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        RequireText(request.Brief, 8000, "brief");
        var p = new VideoProject { Brief = request.Brief.Trim() };
        db.Add(p);
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<ProjectDto[]> ListAsync(CancellationToken ct)
    {
        var ids = await db
            .Projects.OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => p.Id)
            .Take(50)
            .ToArrayAsync(ct);
        var list = new List<ProjectDto>();
        foreach (var id in ids)
            list.Add(await GetAsync(id, ct));
        return list.ToArray();
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await FindAsync(id, ct);
        var jobs = await db
            .Jobs.Where(j => j.ProjectId == id)
            .OrderBy(j => j.CreatedAtUtc)
            .ToArrayAsync(ct);
        var clips = await db
            .Clips.Where(c => c.ProjectId == id)
            .OrderBy(c => c.CreatedAtUtc)
            .ToArrayAsync(ct);
        var links = await db
            .RemixLinks.Where(l => clips.Select(c => c.Id).Contains(l.ResultClipId))
            .ToArrayAsync(ct);
        return new(
            p.Id,
            p.Brief,
            p.State,
            p.Revision,
            p.Confirmed,
            p.StoryboardJson is null
                ? null
                : JsonSerializer.Deserialize<Storyboard>(p.StoryboardJson, Json),
            jobs.Select(j => new JobDto(
                    j.Id,
                    j.Kind,
                    j.State,
                    j.ProviderId,
                    j.ErrorCode,
                    j.Attempt,
                    j.InputTokens,
                    j.OutputTokens
                ))
                .ToArray(),
            clips
                .Select(c => new ClipDto(
                    c.Id,
                    c.JobId,
                    $"/api/video/clips/{c.Id}/content",
                    c.DurationMs,
                    c.Width,
                    c.Height,
                    c.Sha256,
                    c.Prompt,
                    c.Deployment,
                    links.FirstOrDefault(l => l.ResultClipId == c.Id)?.SourceClipId
                ))
                .ToArray()
        );
    }

    public async Task<GenerationJob> StoryboardAsync(
        Guid id,
        StoryboardRequest r,
        CancellationToken ct
    )
    {
        var old = await ExistingAsync(id, r.RequestId, ct);
        if (old is not null)
            return old;
        var p = await FindAsync(id, ct);
        await MutateAsync(p, r.Revision, ct);
        p.Confirmed = false;
        p.State = "Storyboarding";
        var job = new GenerationJob
        {
            ProjectId = id,
            RequestId = r.RequestId,
            InputJson = JsonSerializer.Serialize(new { p.Brief }, Json),
        };
        db.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<ProjectDto> ConfirmAsync(
        Guid id,
        UpdateStoryboardRequest r,
        CancellationToken ct
    )
    {
        ValidateScenes(r.Scenes);
        var p = await FindAsync(id, ct);
        await MutateAsync(p, r.Revision, ct);
        if (p.StoryboardJson is null)
            throw new AppException(409, "storyboard_missing", "Generate a storyboard first.");
        var old = JsonSerializer.Deserialize<Storyboard>(p.StoryboardJson, Json)!;
        p.StoryboardJson = JsonSerializer.Serialize(old with { Scenes = r.Scenes }, Json);
        p.Confirmed = true;
        p.State = "DraftReady";
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<GenerationJob> GenerateAsync(Guid id, GenerateRequest r, CancellationToken ct)
    {
        var old = await ExistingAsync(id, r.RequestId, ct);
        if (old is not null)
            return old;
        var p = await FindAsync(id, ct);
        await MutateAsync(p, r.Revision, ct);
        if (!p.Confirmed || p.StoryboardJson is null)
            throw new AppException(
                409,
                "confirmation_required",
                "Confirm the storyboard before generating a clip."
            );
        var scene =
            JsonSerializer
                .Deserialize<Storyboard>(p.StoryboardJson, Json)!
                .Scenes.SingleOrDefault(s => s.Id == r.SceneId)
            ?? throw new AppException(400, "scene_invalid", "Unknown scene.");
        var caps = await ai.CapabilitiesAsync(ct);
        ValidateParameters(caps, r.Seconds, r.Size);
        return await QueueAsync(
            p,
            r.RequestId,
            new(scene.Prompt, r.Seconds, r.Size, caps.Deployment),
            null,
            ct
        );
    }

    public async Task<GenerationJob> RemixAsync(Guid clipId, RemixRequest r, CancellationToken ct)
    {
        RequireText(r.Prompt, 4000, "prompt");
        var clip =
            await db.Clips.FindAsync([clipId], ct)
            ?? throw new AppException(404, "clip_missing", "Clip not found.");
        var old = await ExistingAsync(clip.ProjectId, r.RequestId, ct);
        if (old is not null)
            return old;
        var p = await FindAsync(clip.ProjectId, ct);
        await MutateAsync(p, r.Revision, ct);
        var caps = await ai.CapabilitiesAsync(ct);
        if (
            (caps.Mode == "Demo") != clip.ProviderId.StartsWith("demo_", StringComparison.Ordinal)
            || caps.Deployment != clip.Deployment
        )
            throw new AppException(
                409,
                "remix_source_ineligible",
                "Select a clip generated by the currently configured provider and deployment."
            );

        ValidateParameters(caps, clip.DurationMs / 1000, $"{clip.Width}x{clip.Height}");
        return await QueueAsync(
            p,
            r.RequestId,
            new(
                r.Prompt.Trim(),
                clip.DurationMs / 1000,
                $"{clip.Width}x{clip.Height}",
                caps.Deployment,
                clip.ProviderId
            ),
            clipId,
            ct
        );
    }

    public async Task<GenerationJob> RetryAsync(
        Guid jobId,
        Guid requestId,
        long revision,
        bool acknowledgeCost,
        CancellationToken ct
    )
    {
        var job =
            await db.Jobs.FindAsync([jobId], ct)
            ?? throw new AppException(404, "job_missing", "Job not found.");
        var old = await ExistingAsync(job.ProjectId, requestId, ct);
        if (old is not null)
            return old;
        if (job.State is not ("Uncertain" or "Failed"))
            throw new AppException(
                409,
                "retry_invalid",
                "Only failed or uncertain jobs can be retried."
            );
        if (job.State == "Uncertain" && !acknowledgeCost)
            throw new AppException(
                409,
                "cost_confirmation",
                "The previous submit may have incurred a charge. Explicit cost acknowledgment is required."
            );
        var p = await FindAsync(job.ProjectId, ct);
        await MutateAsync(p, revision, ct);
        if (job.Kind == "Storyboard")
            return await QueueStoryboardRetryAsync(p, job, requestId, ct);
        return await QueueAsync(
            p,
            requestId,
            JsonSerializer.Deserialize<VideoSubmit>(job.InputJson, Json)!,
            job.ParentClipId,
            ct
        );
    }

    private async Task<GenerationJob> QueueStoryboardRetryAsync(
        VideoProject p,
        GenerationJob source,
        Guid requestId,
        CancellationToken ct
    )
    {
        var j = new GenerationJob
        {
            ProjectId = p.Id,
            RequestId = requestId,
            InputJson = source.InputJson,
        };
        p.State = "Storyboarding";
        db.Add(j);
        await db.SaveChangesAsync(ct);
        return j;
    }

    private async Task<GenerationJob> QueueAsync(
        VideoProject p,
        Guid requestId,
        VideoSubmit input,
        Guid? parent,
        CancellationToken ct
    )
    {
        var j = new GenerationJob
        {
            ProjectId = p.Id,
            RequestId = requestId,
            Kind = parent is null ? "Video" : "Remix",
            ParentClipId = parent,
            InputJson = JsonSerializer.Serialize(input, Json),
        };
        p.State = "Queued";
        db.Add(j);
        db.Add(
            new RunSnapshot
            {
                JobId = j.Id,
                Deployment = input.Deployment,
                ParametersJson = j.InputJson,
            }
        );
        await db.SaveChangesAsync(ct);
        return j;
    }

    private async Task MutateAsync(VideoProject p, long revision, CancellationToken ct)
    {
        if (p.Revision != revision)
            throw new AppException(
                409,
                "revision_conflict",
                "Reload the project; its revision changed."
            );
        if (
            await db.Jobs.AnyAsync(
                j =>
                    j.ProjectId == p.Id
                    && !new[]
                    {
                        "Ready",
                        "Failed",
                        "Canceled",
                        "Uncertain",
                        "NeedsReview",
                    }.Contains(j.State),
                ct
            )
        )
            throw new AppException(409, "job_active", "A job is already active.");
        p.Revision++;
    }

    private Task<VideoProject> FindAsync(Guid id, CancellationToken ct) => FindCoreAsync(id, ct);

    private async Task<VideoProject> FindCoreAsync(Guid id, CancellationToken ct) =>
        await db.Projects.FindAsync([id], ct)
        ?? throw new AppException(404, "project_missing", "Project not found.");

    private async Task<GenerationJob?> ExistingAsync(Guid id, Guid requestId, CancellationToken ct)
    {
        if (requestId == Guid.Empty)
            throw new AppException(400, "request_id_required", "Provide a nonempty requestId.");
        return await db.Jobs.SingleOrDefaultAsync(
            j => j.ProjectId == id && j.RequestId == requestId,
            ct
        );
    }

    public static void RequireText(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            throw new AppException(
                400,
                "invalid_input",
                $"{field} must contain 1–{max} characters."
            );
    }

    public static void ValidateScenes(Scene[]? scenes)
    {
        if (
            scenes is null
            || scenes.Length is < 1 or > 6
            || scenes.Any(s => s is null)
            || scenes.Select(s => s.Id).Distinct().Count() != scenes.Length
        )
            throw new AppException(400, "invalid_scenes", "Provide 1–6 distinct scenes.");
        foreach (var s in scenes)
        {
            RequireText(s.Id, 80, "scene id");
            RequireText(s.Title, 200, "title");
            RequireText(s.Prompt, 4000, "prompt");
        }
    }

    public static void ValidateParameters(Capabilities caps, int seconds, string size)
    {
        if (!caps.VideoEnabled)
            throw new AppException(503, "video_disabled", "Video generation is disabled.");
        if (!caps.Seconds.Contains(seconds) || !caps.Sizes.Contains(size))
            throw new AppException(
                400,
                "unsupported_parameters",
                "Duration or size is not supported by this deployment."
            );
    }
}
