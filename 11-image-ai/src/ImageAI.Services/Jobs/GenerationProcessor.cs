using System.Security.Cryptography;
using System.Text.Json;
using ImageAI.Contracts.Dto;
using ImageAI.Database;
using ImageAI.Domain.Entities;
using ImageAI.Infrastructure.Storage;
using ImageAI.Infrastructure.Validation;
using ImageAI.Services.Gateway;
using Microsoft.EntityFrameworkCore;

namespace ImageAI.Services.Jobs;

public sealed class GenerationProcessor(AppDbContext db, ObjectStore store, DomainAiClient gateway)
{
    public async Task ProcessAsync(GenerationJob claimed, CancellationToken ct)
    {
        var run = await db.Runs.SingleAsync(x => x.Id == claimed.RunId, ct);
        var p = await db.Projects.SingleAsync(x => x.Id == run.ProjectId, ct);
        run.State = p.State = run.Kind == "Edit" ? "Editing" : "Generating";
        p.Revision++;
        await db.SaveChangesAsync(ct);
        byte[]? image = null,
            mask = null;
        if (run.InputAssetId is { } assetId)
            image = await store.ReadAsync(
                (await db.Assets.SingleAsync(x => x.Id == assetId, ct)).ObjectKey,
                ct
            );
        if (run.InputDocumentId is { } docId)
            image = await store.ReadAsync(
                (await db.Documents.SingleAsync(x => x.Id == docId, ct)).ObjectKey,
                ct
            );
        if (run.MaskDocumentId is { } maskId)
            mask = await store.ReadAsync(
                (await db.Documents.SingleAsync(x => x.Id == maskId, ct)).ObjectKey,
                ct
            );
        var output = await gateway.GenerateAsync(
            new(
                run.Id,
                run.Prompt,
                run.Size,
                run.Quality,
                image is null ? null : Convert.ToBase64String(image),
                mask is null ? null : Convert.ToBase64String(mask)
            ),
            ct
        );
        if (output.ImageBase64.Length > 28_000_000)
            throw new GatewayException("provider_image_size_limit", uncertain: true);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(output.ImageBase64);
        }
        catch (FormatException)
        {
            throw new GatewayException("provider_invalid_base64", uncertain: true);
        }
        var info = ImageValidator.Inspect(bytes);
        if ($"{info.Width}x{info.Height}" != run.Size || info.ContentType != "image/png")
            throw new GatewayException("provider_image_dimensions", uncertain: true);
        var key = $"assets/{run.ProjectId:N}/{run.Id:N}.png";
        await store.PutAsync(key, bytes, "image/png", ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            int owns = await db
                .Jobs.Where(x =>
                    x.Id == claimed.Id
                    && x.LeaseToken == claimed.LeaseToken
                    && x.State == "Running"
                    && x.LeaseUntilUtc > DateTimeOffset.UtcNow
                )
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Ready"), ct);
            if (owns != 1)
                throw new GatewayException("lease_lost", uncertain: true);
            db.Assets.Add(
                new ImageAsset
                {
                    ProjectId = run.ProjectId,
                    RunId = run.Id,
                    ParentAssetId = run.InputAssetId,
                    ObjectKey = key,
                    Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    Width = info.Width,
                    Height = info.Height,
                }
            );
            db.Snapshots.Add(
                new GenerationSnapshot
                {
                    RunId = run.Id,
                    Deployment = output.Deployment,
                    UsageJson = output.UsageJson,
                    ProviderRequestId = output.ProviderRequestId,
                    SafetyStatus = output.SafetyStatus,
                    ParametersJson = JsonSerializer.Serialize(
                        new
                        {
                            run.Prompt,
                            PreparedPrompt = output.PreparedPrompt,
                            run.Size,
                            run.Quality,
                            run.InputAssetId,
                            run.InputDocumentId,
                            run.MaskDocumentId,
                        }
                    ),
                }
            );
            run.State = p.State = "Ready";
            p.Revision++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await store.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task FailAsync(GenerationJob claimed, Exception exception, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var e = exception as GatewayException;
        var state =
            e?.Message == "provider_safety_blocked" ? "Blocked"
            : e?.Retryable == true && claimed.Attempt < 3 ? "RetryWaiting"
            : e?.Uncertain == true
            || exception is OperationCanceledException or HttpRequestException
                ? "Uncertain"
            : "Failed";
        var delay = Math.Max(
            e?.RetryAfterSeconds ?? 0,
            (int)Math.Pow(2, claimed.Attempt) + Random.Shared.Next(1, 5)
        );
        var next = DateTimeOffset.UtcNow.AddSeconds(delay);
        var owns = await db
            .Jobs.Where(x =>
                x.Id == claimed.Id && x.LeaseToken == claimed.LeaseToken && x.State == "Running"
            )
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.State, state).SetProperty(x => x.NextAttemptAtUtc, next),
                ct
            );
        if (owns != 1)
            return;
        var run = await db.Runs.SingleAsync(x => x.Id == claimed.RunId, ct);
        var p = await db.Projects.SingleAsync(x => x.Id == run.ProjectId, ct);
        run.State = state;
        p.State = state == "RetryWaiting" ? "Queued" : state;
        run.ErrorCode = e?.Message ?? "generation_failed";
        p.Revision++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
