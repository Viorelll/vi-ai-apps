using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageAI.Contracts.Dto;
using ImageAI.Database;
using ImageAI.Domain.Entities;
using ImageAI.Domain.Models;
using ImageAI.Infrastructure.Storage;
using ImageAI.Infrastructure.Validation;
using ImageAI.Services.Validation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ImageAI.Services.Images;

public sealed class ImageProjectService(AppDbContext db, ObjectStore store)
{
    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var p = new ImageProject { Brief = RequestValidator.Brief(request.Brief) };
        db.Projects.Add(p);
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.Id, ct);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct)
    {
        var ids = await db
            .Projects.OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var items = new List<ProjectDto>();
        foreach (var id in ids)
            items.Add(await GetAsync(id, ct));
        return items;
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct)
    {
        var p =
            await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("project_not_found", 404);
        var assets = await db
            .Assets.Where(x => x.ProjectId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new AssetDto(
                x.Id,
                x.RunId,
                x.ParentAssetId,
                x.Width,
                x.Height,
                x.Sha256,
                x.CreatedAtUtc
            ))
            .ToListAsync(ct);
        var runs = await db
            .Runs.Where(x => x.ProjectId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);
        var snapshots = await db
            .Snapshots.Where(x => runs.Select(r => r.Id).Contains(x.RunId))
            .ToListAsync(ct);
        return new(
            p.Id,
            p.Brief,
            p.State,
            p.Revision,
            assets,
            runs.Select(x =>
                {
                    var s = snapshots.SingleOrDefault(s => s.RunId == x.Id);
                    return new RunDto(
                        x.Id,
                        x.Kind,
                        x.State,
                        x.ErrorCode,
                        x.Prompt,
                        x.InputAssetId,
                        x.InputDocumentId,
                        x.MaskDocumentId,
                        x.Size,
                        x.Quality,
                        s?.Deployment,
                        s?.UsageJson
                    );
                })
                .ToList()
        );
    }

    public async Task<QueuedRunDto> GenerateAsync(
        Guid id,
        GenerateRequest request,
        CancellationToken ct
    )
    {
        var p = await FindAsync(id, ct);
        if (
            request.InputDocumentId is { } doc
            && !await db.Documents.AnyAsync(x => x.Id == doc, ct)
        )
            throw new DomainException("document_not_found", 404);
        return await QueueAsync(
            p,
            request.RequestId,
            request.Revision,
            new ImageRun
            {
                ProjectId = id,
                Prompt = p.Brief,
                Size = request.Size,
                Quality = request.Quality,
                InputDocumentId = request.InputDocumentId,
            },
            ct
        );
    }

    public async Task<QueuedRunDto> EditAsync(Guid id, EditRequest request, CancellationToken ct)
    {
        var p = await FindAsync(id, ct);
        var asset = await OwnedAssetAsync(id, request.InputAssetId, ct);
        var d =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == request.MaskDocumentId, ct)
            ?? throw new DomainException("mask_document_not_found", 404);
        ImageValidator.ValidateMask(
            asset.Width,
            asset.Height,
            await store.ReadAsync(d.ObjectKey, ct)
        );
        return await QueueAsync(
            p,
            request.RequestId,
            request.Revision,
            new ImageRun
            {
                ProjectId = id,
                Kind = "Edit",
                Prompt = RequestValidator.Brief(request.Prompt),
                Size = $"{asset.Width}x{asset.Height}",
                Quality = request.Quality,
                InputAssetId = asset.Id,
                MaskDocumentId = d.Id,
            },
            ct
        );
    }

    public async Task<QueuedRunDto> VariantAsync(
        Guid id,
        VariantRequest request,
        CancellationToken ct
    )
    {
        var p = await FindAsync(id, ct);
        var asset = await OwnedAssetAsync(id, request.AssetId, ct);
        return await QueueAsync(
            p,
            request.RequestId,
            request.Revision,
            new ImageRun
            {
                ProjectId = id,
                Kind = "Variant",
                Prompt = RequestValidator.Brief(request.Brief),
                Size = $"{asset.Width}x{asset.Height}",
                Quality = request.Quality,
                InputAssetId = asset.Id,
            },
            ct
        );
    }

    public async Task<ImageAsset> AssetAsync(Guid id, CancellationToken ct) =>
        await db.Assets.FindAsync([id], ct) ?? throw new DomainException("asset_not_found", 404);

    private async Task<ImageAsset> OwnedAssetAsync(
        Guid projectId,
        Guid assetId,
        CancellationToken ct
    ) =>
        await db.Assets.SingleOrDefaultAsync(x => x.Id == assetId && x.ProjectId == projectId, ct)
        ?? throw new DomainException("asset_not_in_project", 404);

    private async Task<ImageProject> FindAsync(Guid id, CancellationToken ct) =>
        await db.Projects.FindAsync([id], ct)
        ?? throw new DomainException("project_not_found", 404);

    private async Task<QueuedRunDto> QueueAsync(
        ImageProject p,
        Guid requestId,
        int revision,
        ImageRun run,
        CancellationToken ct
    )
    {
        RequestValidator.RequestId(requestId);
        RequestValidator.Parameters(run.Size, run.Quality);
        run.RequestId = requestId;
        run.RequestHash = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        new
                        {
                            run.ProjectId,
                            run.Kind,
                            run.Prompt,
                            run.Size,
                            run.Quality,
                            run.InputAssetId,
                            run.InputDocumentId,
                            run.MaskDocumentId,
                        }
                    )
                )
            )
        );
        var prior = await db.Runs.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        if (prior is not null)
            return await ExistingAsync(prior, run, ct);
        if (p.Revision != revision)
            throw new DomainException("revision_conflict", 409);
        if (p.State is "Queued" or "Generating" or "Editing")
            throw new DomainException("project_busy", 409);
        if (await db.Runs.CountAsync(x => x.ProjectId == p.Id && x.State == "Ready", ct) >= 12)
            throw new DomainException("project_variant_limit", 409);
        if (
            run.MaskDocumentId is { } maskId
            && run.InputAssetId is { } inputId
            && !await db.Masks.AnyAsync(
                x => x.DocumentId == maskId && x.InputAssetId == inputId,
                ct
            )
        )
        {
            var d = await db.Documents.SingleAsync(x => x.Id == maskId, ct);
            db.Masks.Add(
                new EditMask
                {
                    DocumentId = maskId,
                    InputAssetId = inputId,
                    Width = d.Width,
                    Height = d.Height,
                    RegionHash = d.Sha256,
                }
            );
        }
        var job = new GenerationJob { RunId = run.Id };
        p.State = "Queued";
        p.Revision++;
        db.Runs.Add(run);
        db.Jobs.Add(job);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DomainException("revision_conflict", 409);
        }
        catch (DbUpdateException e)
            when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            var found = await db.Runs.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
            if (found is not null)
                return await ExistingAsync(found, run, ct);
            throw new DomainException("concurrent_request", 409);
        }
        return new(p.Id, run.Id, job.Id, run.State);
    }

    private async Task<QueuedRunDto> ExistingAsync(
        ImageRun prior,
        ImageRun proposed,
        CancellationToken ct
    )
    {
        if (prior.RequestHash != proposed.RequestHash)
            throw new DomainException("request_id_payload_conflict", 409);
        var job = await db.Jobs.SingleAsync(x => x.RunId == prior.Id, ct);
        return new(prior.ProjectId, prior.Id, job.Id, prior.State);
    }
}
