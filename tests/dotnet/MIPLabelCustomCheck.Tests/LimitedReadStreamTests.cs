using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class LimitedReadStreamTests
{
    [Fact]
    public void Read_array_tracks_consumed_bytes_and_dispose_leaves_inner_open()
    {
        var inner = new MemoryStream(new byte[] { 1, 2, 3 });
        var stream = new LimitedReadStream(inner, maximumBytes: 2);
        var buffer = new byte[2];

        Assert.Equal(2, stream.Read(buffer, 0, buffer.Length));
        Assert.Throws<InvalidDataException>(() => stream.Read(buffer, 0, 1));

        stream.Dispose();
        Assert.True(inner.CanRead);
        inner.Dispose();
    }

    [Fact]
    public void Read_span_tracks_consumed_bytes()
    {
        using var inner = new MemoryStream(new byte[] { 1, 2 });
        using var stream = new LimitedReadStream(inner, maximumBytes: 1);
        Span<byte> buffer = stackalloc byte[1];

        Assert.Equal(1, stream.Read(buffer));
        Assert.Throws<InvalidDataException>(() => stream.Read(new byte[1].AsSpan()));
    }

    [Fact]
    public void ReadByte_tracks_consumed_bytes()
    {
        using var inner = new MemoryStream(new byte[] { 1, 2 });
        using var stream = new LimitedReadStream(inner, maximumBytes: 1);

        Assert.Equal(1, stream.ReadByte());
        Assert.Throws<InvalidDataException>(() => stream.ReadByte());
    }

    [Fact]
    public void Reads_after_limit_exception_do_not_advance_inner_stream()
    {
        using var inner = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using var stream = new LimitedReadStream(inner, maximumBytes: 1);
        var buffer = new byte[1];

        Assert.Equal(1, stream.ReadByte());
        Assert.Throws<InvalidDataException>(() => stream.ReadByte());
        var positionAfterOverflow = inner.Position;

        Assert.Throws<InvalidDataException>(() => stream.Read(buffer.AsSpan()));
        Assert.Equal(positionAfterOverflow, inner.Position);
        Assert.Throws<InvalidDataException>(() => stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(positionAfterOverflow, inner.Position);
        Assert.Throws<InvalidDataException>(() => stream.ReadByte());
        Assert.Equal(positionAfterOverflow, inner.Position);
    }

    [Fact]
    public void ReadByte_returns_end_of_stream_at_exact_limit()
    {
        using var inner = new MemoryStream(new byte[] { 1 });
        using var stream = new LimitedReadStream(inner, maximumBytes: 1);

        Assert.Equal(1, stream.ReadByte());
        Assert.Equal(-1, stream.ReadByte());
    }
}
