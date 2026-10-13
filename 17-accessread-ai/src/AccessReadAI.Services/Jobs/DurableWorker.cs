using AccessReadAI.Contracts;
using AccessReadAI.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccessReadAI.Services.Jobs;

public sealed class DurableWorker(IServiceScopeFactory scopes, ILogger<DurableWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var job = await scope
                    .ServiceProvider.GetRequiredService<JobStore>()
                    .ClaimAsync(stoppingToken);
                if (job == null)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(
                        stoppingToken
                    );
                    deadline.CancelAfter(TimeSpan.FromSeconds(240));
                    await scope
                        .ServiceProvider.GetRequiredService<PipelineProcessor>()
                        .ProcessAsync(job, deadline.Token);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (DbUpdateConcurrencyException)
                {
                    logger.LogWarning("Job lease or reading revision changed: {JobId}", job.Id);
                }
                catch (Exception error)
                {
                    var transient =
                        error
                        is ProviderFault { Transient: true }
                            or HttpRequestException
                            or TaskCanceledException;
                    var code = error is ProviderFault ? error.Message : "pipeline_failure";
                    // Discard incomplete tracked outputs; persist only job outcome under the original lease.
                    var token = job.LeaseToken;
                    var jobId = job.Id;
                    db.ChangeTracker.Clear();
                    var current = await db.Jobs.SingleOrDefaultAsync(
                        x => x.Id == jobId && x.LeaseToken == token && x.State == "Running",
                        stoppingToken
                    );
                    if (current == null)
                        continue;
                    current.Attempt++;
                    current.ErrorCode = code;
                    if (transient && current.Attempt < 3)
                    {
                        current.State = "RetryWaiting";
                        current.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(
                            Math.Max(
                                error is ProviderFault f ? f.RetryAfterSeconds : 0,
                                Math.Pow(2, current.Attempt) * 2 + Random.Shared.Next(3)
                            )
                        );
                    }
                    else
                    {
                        current.State = "Failed";
                        if (current.TargetId.HasValue)
                        {
                            var reading = await db.Readings.FindAsync(
                                [current.TargetId],
                                stoppingToken
                            );
                            if (reading != null)
                            {
                                reading.State = "Failed";
                                reading.ErrorCode = code;
                                reading.Revision++;
                            }
                        }
                        else
                        {
                            var doc = await db.Documents.FindAsync(
                                [current.DocumentId],
                                stoppingToken
                            );
                            if (doc != null && doc.State != "Deleting")
                            {
                                doc.State = "Failed";
                                doc.ErrorCode = code;
                            }
                        }
                    }
                    current.LeaseUntilUtc = null;
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogWarning(
                        "Job {JobId} failed with {Code}; attempt {Attempt}",
                        jobId,
                        code,
                        current.Attempt
                    );
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogWarning("Worker database temporarily unavailable");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
