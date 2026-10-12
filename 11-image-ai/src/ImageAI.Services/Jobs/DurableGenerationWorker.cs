using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImageAI.Services.Jobs;

public sealed class DurableGenerationWorker(
    IServiceScopeFactory scopes,
    ILogger<DurableGenerationWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var leaser = scope.ServiceProvider.GetRequiredService<JobLeaser>();
                await leaser.RecoverExpiredAsync(stoppingToken);
                var job = await leaser.ClaimAsync(stoppingToken);
                if (job is not null)
                {
                    var processor = scope.ServiceProvider.GetRequiredService<GenerationProcessor>();
                    try
                    {
                        await processor.ProcessAsync(job, stoppingToken);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(
                            "Generation failed for run {RunId}: {Type}",
                            job.RunId,
                            e.GetType().Name
                        );
                        await processor.FailAsync(job, e, CancellationToken.None);
                    }
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError("Worker prerequisite failed: {Type}", e.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
