using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;

namespace ProcureAI.Application.Services;

public sealed class DurableWorker(IServiceScopeFactory scopes, ILogger<DurableWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IJobStore>();
                var repository = scope.ServiceProvider.GetRequiredService<IProcurementRepository>();
                var job = await jobs.ClaimAsync(stoppingToken);
                if (job is null) { await Task.Delay(500, stoppingToken); continue; }
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(120));
                    if (job.RunId is not null)
                        await scope.ServiceProvider.GetRequiredService<WorkflowProcessor>().ProcessAsync(job, deadline.Token);
                    else
                        await scope.ServiceProvider.GetRequiredService<DocumentProcessor>().ProcessAsync(job, deadline.Token);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    var appException = exception as AppException;
                    var code = appException?.Code ?? (exception is OperationCanceledException ? "provider_timeout" : "processing_failed");
                    var transient = exception is HttpRequestException or OperationCanceledException || appException?.Status is 429 or 503;
                    var retry = transient && !code.EndsWith("not_configured", StringComparison.Ordinal) && job.Attempt < 2;
                    logger.LogWarning("Job {JobId} failed: {Code}; retry {Retry}", job.Id, code, retry);
                    if (job.RunId is not null && !retry)
                        await repository.StopRunAsync(job, appException?.Status == 422 ? "NeedsInput" : "Failed", code, stoppingToken);
                    else
                        await jobs.ReleaseAsync(job, retry ? "RetryWaiting" : "Failed",
                            TimeSpan.FromSeconds(Math.Pow(2, job.Attempt + 1) + Random.Shared.NextDouble()), code, job.ProviderOperation, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Worker temporarily unavailable: {ErrorType}", exception.GetType().Name);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}
