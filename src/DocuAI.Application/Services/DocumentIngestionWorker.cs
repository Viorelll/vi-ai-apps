using System.Text;
using DocuAI.Application.Abstractions;
using DocuAI.Application.Exceptions;
using DocuAI.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocuAI.Application.Services;

public sealed class DocumentIngestionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentIngestionWorker> logger) : BackgroundService
{
    private const int ChunkSize = 2800;
    private const int ChunkStep = 2400;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
                var objectStore = scope.ServiceProvider.GetRequiredService<IObjectStore>();
                var extractionClient = scope.ServiceProvider.GetRequiredService<IAiExtractionClient>();
                var claim = await queue.ClaimNextAsync(DateTimeOffset.UtcNow, stoppingToken);

                if (claim is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                var (job, document) = claim.Value;
                try
                {
                    await using var source = await objectStore.OpenReadAsync(document.ObjectKey, stoppingToken);
                    string text;
                    var extension = Path.GetExtension(document.FileName);
                    if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                        text = await reader.ReadToEndAsync(stoppingToken);
                    }
                    else
                    {
                        text = await extractionClient.ExtractAsync(document.FileName, source, stoppingToken);
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        throw new InvalidOperationException("Empty extraction result.");
                    }

                    var chunks = CreateChunks(document.Id, text);
                    document.Status = DocumentStatus.Ready;
                    document.ErrorCode = null;
                    job.Status = IngestionJobStatus.Complete;
                    job.LeaseUntilUtc = null;
                    await queue.CompleteAsync(job, document, chunks, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    document.Status = DocumentStatus.Failed;
                    document.ErrorCode = exception is AiExtractionException
                        ? "FOUNDRY_EXTRACTION_NOT_CONFIGURED"
                        : "EXTRACTION_FAILED";
                    job.Status = IngestionJobStatus.Failed;
                    job.LeaseUntilUtc = null;
                    logger.LogWarning("Document ingestion failed with error code {ErrorCode}", document.ErrorCode);
                    await queue.FailAsync(job, document, document.ErrorCode, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The document ingestion worker encountered an error.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private static IReadOnlyList<DocumentChunk> CreateChunks(Guid documentId, string text)
    {
        var chunks = new List<DocumentChunk>();
        for (var offset = 0; offset < text.Length; offset += ChunkStep)
        {
            var length = Math.Min(ChunkSize, text.Length - offset);
            chunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                Generation = 1,
                Ordinal = chunks.Count,
                Text = text.Substring(offset, length)
            });
        }

        return chunks;
    }
}
