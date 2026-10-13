using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using VideoSearchAI.Domain.Entities;
using VideoSearchAI.Infrastructure.Storage;

namespace VideoSearchAI.Api.Results;

public sealed class VideoContentResult(Document document) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext action)
    {
        var c = action.HttpContext;
        var response = c.Response;
        var request = c.Request;
        var ct = c.RequestAborted;
        var etag = $"\"{document.Sha256}\"";
        response.Headers.ETag = etag;
        response.Headers.AcceptRanges = "bytes";
        response.Headers.CacheControl = "private, no-cache";
        response.Headers.XContentTypeOptions = "nosniff";
        if (request.Headers.IfNoneMatch.Any(x => x == etag))
        {
            response.StatusCode = 304;
            return;
        }
        long start = 0,
            end = document.SizeBytes - 1;
        var partial = false;
        if (
            request.Headers.TryGetValue("Range", out var range)
            && (!request.Headers.TryGetValue("If-Range", out var ifRange) || ifRange == etag)
        )
        {
            if (
                !RangeHeaderValue.TryParse(range.ToString(), out var parsed)
                || parsed.Unit != "bytes"
                || parsed.Ranges.Count != 1
            )
            {
                await InvalidRange();
                return;
            }
            var item = parsed.Ranges.Single();
            if (item.From.HasValue)
            {
                start = item.From.Value;
                end = Math.Min(item.To ?? end, end);
            }
            else if (item.To.HasValue && item.To > 0)
            {
                start = Math.Max(0, document.SizeBytes - item.To.Value);
            }
            else
            {
                await InvalidRange();
                return;
            }
            if (start < 0 || start >= document.SizeBytes || end < start)
            {
                await InvalidRange();
                return;
            }
            partial = true;
        }
        response.StatusCode = partial ? 206 : 200;
        response.ContentType = document.ContentType;
        response.ContentLength = end - start + 1;
        if (partial)
            response.Headers.ContentRange = $"bytes {start}-{end}/{document.SizeBytes}";
        if (HttpMethods.IsHead(request.Method))
            return;
        var s3 = c.RequestServices.GetRequiredService<IAmazonS3>();
        var o = c.RequestServices.GetRequiredService<IOptions<StorageOptions>>().Value;
        using var obj = await s3.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = o.Bucket,
                Key = document.ObjectKey,
                ByteRange = new ByteRange(start, end),
            },
            ct
        );
        await obj.ResponseStream.CopyToAsync(response.Body, ct);
        Task InvalidRange()
        {
            response.StatusCode = 416;
            response.Headers.ContentRange = $"bytes */{document.SizeBytes}";
            response.ContentLength = 0;
            return Task.CompletedTask;
        }
    }
}
