using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoStudioAI.Services;
using VideoStudioAI.Services.Interfaces;

namespace VideoStudioAI.Infrastructure.Workers;

public sealed class DurableWorker(
    JobLeaseService leases,
    VideoJobProcessor video,
    IngestionProcessor documents,
    IObjectStore store,
    ILogger<DurableWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await store.EnsureBucketAsync(stoppingToken);
                var job = await leases.ClaimVideoAsync(stoppingToken);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(55));
                if (job is not null)
                    await video.ProcessAsync(job, timeout.Token);
                var doc = await leases.ClaimDocumentAsync(stoppingToken);
                if (doc is not null)
                    await documents.ProcessAsync(doc, timeout.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker iteration failed");
            }
            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
