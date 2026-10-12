namespace ProcureAI.Application.Abstractions;

using ProcureAI.Domain.Entities;

public interface IJobStore
{
    Task<DurableJob?> ClaimAsync(CancellationToken ct);
    Task ReleaseAsync(DurableJob job, string state, TimeSpan delay, string? code, string? operation, CancellationToken ct);
}
