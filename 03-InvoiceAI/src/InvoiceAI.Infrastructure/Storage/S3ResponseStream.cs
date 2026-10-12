using Amazon.S3.Model;
namespace InvoiceAI.Infrastructure.Storage;

public sealed class S3ResponseStream(GetObjectResponse response) : Stream
{
    private Stream Inner => response.ResponseStream;
    public override bool CanRead => Inner.CanRead;
    public override bool CanSeek => Inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => response.ContentLength;
    public override long Position
    {
        get => Inner.Position; set => Inner.Position = value;
    }
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => Inner.ReadAsync(buffer, ct);
    public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
    public override void Flush() => Inner.Flush();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            response.Dispose();
        base.Dispose(disposing);
    }
    public override ValueTask DisposeAsync()
    {
        response.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
