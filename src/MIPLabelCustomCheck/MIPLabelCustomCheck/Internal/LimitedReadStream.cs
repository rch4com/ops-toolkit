namespace MIPLabelCustomCheck.Internal;

internal sealed class LimitedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maximumBytes;
    private long _bytesRead;

    internal LimitedReadStream(Stream inner, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (!inner.CanRead)
        {
            throw new ArgumentException("The inner stream must be readable.", nameof(inner));
        }

        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        _inner = inner;
        _maximumBytes = maximumBytes;
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (offset > buffer.Length - count)
        {
            throw new ArgumentException("Offset and count exceed the buffer bounds.");
        }

        var read = _inner.Read(buffer, offset, GetReadSize(count));
        RecordRead(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer[..GetReadSize(buffer.Length)]);
        RecordRead(read);
        return read;
    }

    public override int ReadByte()
    {
        ThrowIfLimitExceeded();

        var value = _inner.ReadByte();
        if (value >= 0)
        {
            RecordRead(1);
        }

        return value;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing) => base.Dispose(disposing);

    private int GetReadSize(int requestedCount)
    {
        ThrowIfLimitExceeded();

        if (requestedCount == 0)
        {
            return 0;
        }

        var remainingBytes = _maximumBytes - _bytesRead;
        return (int)Math.Min(requestedCount, remainingBytes + 1);
    }

    private void RecordRead(int count)
    {
        _bytesRead += count;
        if (_bytesRead > _maximumBytes)
        {
            throw new InvalidDataException("The stream exceeded the configured byte limit.");
        }
    }

    private void ThrowIfLimitExceeded()
    {
        if (_bytesRead > _maximumBytes)
        {
            throw new InvalidDataException("The stream exceeded the configured byte limit.");
        }
    }
}
