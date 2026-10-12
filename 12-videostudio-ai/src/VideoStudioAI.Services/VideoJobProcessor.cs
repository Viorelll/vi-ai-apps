using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Database;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Domain.Models;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Services;

public sealed class VideoJobProcessor(
    IDbContextFactory<AppDbContext> factory,
    IAiGateway ai,
    IObjectStore store,
    IMediaValidator media,
    ILogger<VideoJobProcessor> logger
)
{
    public async Task ProcessAsync(GenerationJob claimed, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var job = await db.Jobs.SingleAsync(x => x.Id == claimed.Id, ct);
        if (job.LeaseToken != claimed.LeaseToken)
            return;
        var p = await db.Projects.SingleAsync(x => x.Id == job.ProjectId, ct);
        try
        {
            if (DateTimeOffset.UtcNow - job.CreatedAtUtc > TimeSpan.FromMinutes(30))
                throw new AppException(422, "job_deadline", "Generation deadline exceeded.");
            if (job.Kind == "Storyboard")
            {
                job.State = "InProgress";
                await db.SaveChangesAsync(ct);
                var brief = JsonDocument
                    .Parse(job.InputJson)
                    .RootElement.GetProperty("brief")
                    .GetString()!;
                var result = await ai.StoryboardAsync(brief, ct);
                ProjectService.ValidateScenes(result.Scenes);
                if (result.InputTokens + result.OutputTokens > 15000)
                    throw new AppException(422, "token_budget", "Token budget exceeded.");
                p.StoryboardJson = JsonSerializer.Serialize(result, ProjectService.Json);
                p.Confirmed = false;
                p.State = "DraftReady";
                job.InputTokens += result.InputTokens;
                job.OutputTokens += result.OutputTokens;
                job.State = "Ready";
            }
            else
            {
                var input = JsonSerializer.Deserialize<VideoSubmit>(
                    job.InputJson,
                    ProjectService.Json
                )!;
                if (job.ProviderId is null)
                {
                    if (job.Phase == "Submitting")
                    {
                        job.State = "Uncertain";
                        job.ErrorCode = "submit_uncertain";
                    }
                    else
                    {
                        job.Phase = "Submitting";
                        job.State = "InProgress";
                        await db.SaveChangesAsync(ct);
                        var provider = await ai.SubmitAsync(input, ct);
                        if (!VideoRules.ValidProviderId(provider.Id))
                            throw new AppException(
                                502,
                                "provider_id_invalid",
                                "Invalid provider response."
                            );
                        job.ProviderId = provider.Id;
                        job.Phase = "Polling";
                        job.State = VideoRules.MapState(provider.Status);
                        // Persist the provider checkpoint before any polling or download.
                        await db.SaveChangesAsync(ct);
                    }
                }
                else
                {
                    var provider = await ai.PollAsync(job.ProviderId, ct);
                    job.State = VideoRules.MapState(provider.Status);
                    job.ErrorCode = provider.ErrorCode;
                }
                if (job.State == "Downloading")
                {
                    job.Phase = "Downloading";
                    await db.SaveChangesAsync(ct);
                    await using var content = await ai.DownloadAsync(job.ProviderId!, ct);
                    var dims = input.Size.Split('x').Select(int.Parse).ToArray();
                    await media.ValidateAsync(content.Path, input.Seconds, dims[0], dims[1], ct);
                    var key = $"clips/{job.ProjectId}/{job.Id}.mp4";
                    await store.PutAsync(key, content.Path, "video/mp4", ct);
                    var clip = new VideoClip
                    {
                        Id = job.Id,
                        ProjectId = job.ProjectId,
                        JobId = job.Id,
                        ProviderId = job.ProviderId!,
                        ObjectKey = key,
                        Sha256 = content.Sha256,
                        SizeBytes = content.Length,
                        DurationMs = input.Seconds * 1000,
                        Width = dims[0],
                        Height = dims[1],
                        Prompt = input.Prompt,
                        Deployment = input.Deployment,
                    };
                    if (!await db.Clips.AnyAsync(c => c.JobId == job.Id, ct))
                    {
                        db.Add(clip);
                        if (job.ParentClipId is Guid parent)
                            db.Add(
                                new RemixLink
                                {
                                    SourceClipId = parent,
                                    ResultClipId = clip.Id,
                                    Prompt = input.Prompt,
                                }
                            );
                    }
                    job.State = "Ready";
                    job.Phase = "Complete";
                }
                p.State = job.State;
            }
            job.ErrorCode = job.State == "Ready" ? null : job.ErrorCode;
            job.PollCount++;
            job.NextPollAtUtc = DateTimeOffset.UtcNow + VideoRules.PollDelay(job.PollCount);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Lease or revision lost for {JobId}", job.Id);
            return;
        }
        catch (Exception ex)
            when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            HandleFailure(job, ex);
            p.State = job.State;
            logger.LogWarning(ex, "Job {JobId} failed at {Phase}", job.Id, job.Phase);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (job.Phase == "Submitting" && job.ProviderId is null)
            {
                job.State = "Uncertain";
                job.ErrorCode = "submit_uncertain";
                p.State = job.State;
            }
            else
            {
                job.NextPollAtUtc = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
            }
        }
        p.Revision++;
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Lost lease while finalizing {JobId}", job.Id);
        }
    }

    public static void HandleFailure(GenerationJob job, Exception ex)
    {
        var app = ex as AppException;
        if (
            job.Phase == "Submitting"
            && job.ProviderId is null
            && (app is null || app.Status >= 500)
        )
        {
            job.State = "Uncertain";
            job.ErrorCode = "submit_uncertain";
            return;
        }
        job.Attempt++;
        job.ErrorCode = app?.Code ?? "gateway_unavailable";
        if (job.Attempt < 3 && (app is null || app.Status is 429 or 502 or 503 or 504))
        {
            job.State = "Queued";
            if (job.ProviderId is null)
                job.Phase = "Pending";
            job.NextPollAtUtc = DateTimeOffset.UtcNow + VideoRules.PollDelay(job.Attempt);
        }
        else
            job.State = "Failed";
    }
}
