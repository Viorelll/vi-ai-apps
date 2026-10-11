using Amazon.S3.Model;

namespace DocuAI.Infrastructure.Storage;

internal sealed class S3ObjectResponseStream(GetObjectResponse response) : Stream
{
    private Stream Inner => response.ResponseStream;

    public override bool CanRead => Inner.CanRead;
    public override bool CanSeek => Inner.CanSeek;
    public override bool CanWrite => Inner.CanWrite;
    public override long Length => Inner.Length;
    public override long Position { get => Inner.Position; set => Inner.Position = value; }

    public override void Flush() => Inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Inner.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
    public override void SetLength(long value) => Inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing) response.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        response.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
