using System.Text;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
using InvoiceAI.Services.Mapping;
namespace InvoiceAI.Api.Workers;

public sealed class DurableInvoiceWorker(IServiceScopeFactory scopes, ILogger<DurableInvoiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
                var job = await jobs.ClaimAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(65));
                var ct = deadline.Token;
                try
                {
                    var storage = scope.ServiceProvider.GetRequiredService<IObjectStore>();
                    var ai = scope.ServiceProvider.GetRequiredService<IAiGateway>();
                    if (job.Kind == "Cleanup")
                    {
                        // Metadata is hidden immediately; original deletion and chunk cleanup are repeatable.
                        var factory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<InvoiceAI.Database.Persistence.AppDbContext>>();
                        await using var db = await factory.CreateDbContextAsync(ct);
                        var doc = await db.Documents.FindAsync([job.DocumentId], ct);
                        if (doc is not null)
                            await storage.DeleteAsync(doc.ObjectKey, ct);
                        var artifacts = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.IngestionJobs.Where(x => x.DocumentId == job.DocumentId && x.RawObjectKey != null).Select(x => x.RawObjectKey!), ct);
                        await storage.DeleteAsync($"artifacts/{job.DocumentId:N}/invoice-v1/raw.json", ct);
                        foreach (var key in artifacts)
                            await storage.DeleteAsync(key, ct);
                        await jobs.FinishCleanupAsync(job, ct);
                    }
                    else if (job.Phase == "Start")
                    {
                        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
                        var invoice = await invoices.GetAsync(job.TargetId!.Value, ct);
                        var start = await ai.StartAsync(new(job.DocumentId, invoice.Document.ObjectKey, invoice.Document.ContentType, invoice.Kind), ct);
                        await jobs.SchedulePollAsync(job, start.OperationUrl, 1, ct);
                    }
                    else if (job.Phase == "Poll")
                    {
                        var invoice = await scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().GetAsync(job.TargetId!.Value, ct);
                        var result = await ai.PollAsync(new(job.DocumentId, job.ProviderOperation!, invoice.Kind), ct);
                        if (result.Status == "running")
                            await jobs.SchedulePollAsync(job, job.ProviderOperation!, result.RetryAfterSeconds, ct);
                        else if (result.Status == "succeeded" && result.Extraction is not null)
                        {
                            var key = $"artifacts/{job.DocumentId:N}/{job.PipelineVersion}/raw.json";
                            await using var raw = new MemoryStream(Encoding.UTF8.GetBytes(result.Extraction.RawJson));
                            await storage.PutAsync(key, raw, "application/json", ct);
                            await jobs.ActivateAsync(job, result.Extraction, key, ct);
                        }
                        else
                            throw new GatewayException("EXTRACTION_FAILED", false);
                    }
                    else if (job.Phase == "Explain")
                    {
                        var invoice = await scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().GetAsync(job.TargetId!.Value, ct);
                        var answer = await ai.ExplainAsync(new(invoice.Id, invoice.Fields.Select(InvoiceMapper.Field).ToArray(), invoice.Lines.Select(InvoiceMapper.Line).ToArray(), invoice.Issues.Select(InvoiceMapper.Issue).ToArray()), ct);
                        await jobs.CompleteAsync(job, answer, ct);
                    }
                    else
                        throw new AppException("JOB_PHASE_INVALID");
                }
                catch (AppException e) when (e.Code == "LEASE_LOST") { logger.LogInformation("Lease lost for job {JobId}", job.Id); }
                catch (Exception e) when (!stoppingToken.IsCancellationRequested)
                {
                    string code;
                    bool transient;
                    int delay;
                    if (e is GatewayException g)
                    {
                        code = g.Code;
                        transient = g.Transient;
                        delay = g.RetryAfterSeconds;
                    }
                    else if (e is AppException a)
                    {
                        code = a.Code;
                        transient = false;
                        delay = 0;
                    }
                    else
                    {
                        code = "DEPENDENCY_UNAVAILABLE";
                        transient = true;
                        delay = 2;
                    }
                    logger.LogWarning("Job {JobId} failed with {Code} (attempt {Attempt})", job.Id, code, job.Attempt + 1);
                    try
                    {
                        await jobs.FailAsync(job, code, transient, Math.Max(delay, (1 << Math.Min(job.Attempt + 1, 5)) + Random.Shared.Next(3)), stoppingToken, e is GatewayException usage ? usage.InputTokens : 0, e is GatewayException usageOut ? usageOut.OutputTokens : 0);
                    }
                    catch (AppException a) when (a.Code == "LEASE_LOST") { }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogWarning("Worker dependency unavailable: {ErrorType}", e.GetType().Name); await Task.Delay(2000, stoppingToken); }
        }
    }
}
