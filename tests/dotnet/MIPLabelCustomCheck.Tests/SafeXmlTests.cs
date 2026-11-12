using System.Text;
using System.Xml;
using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class SafeXmlTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Load_rejects_non_positive_maximum(int maximumBytes)
    {
        using var stream = CreateStream("<root />");

        Assert.Throws<ArgumentOutOfRangeException>(() => SafeXml.Load(stream, maximumBytes));
    }

    [Fact]
    public void Load_rejects_dtd_and_leaves_caller_stream_open()
    {
        const string xml = "<!DOCTYPE root [<!ELEMENT root ANY>]><root />";
        var stream = CreateStream(xml);

        Assert.Throws<XmlException>(() => SafeXml.Load(stream, Encoding.UTF8.GetByteCount(xml)));
        Assert.True(stream.CanRead);

        stream.Dispose();
    }

    [Fact]
    public void Load_enforces_limit_for_non_seekable_stream_and_leaves_it_open()
    {
        const string xml = "<root><value>metadata</value></root>";
        var bytes = Encoding.UTF8.GetBytes(xml);
        var stream = new NonSeekableReadStream(bytes);

        Assert.Throws<InvalidDataException>(() => SafeXml.Load(stream, bytes.Length - 1));
        Assert.False(stream.IsDisposed);

        stream.Dispose();
    }

    [Fact]
    public void Load_accepts_exact_limit_and_leaves_caller_stream_open()
    {
        const string xml = "<root><value>metadata</value></root>";
        var bytes = Encoding.UTF8.GetBytes(xml);
        var stream = new NonSeekableReadStream(bytes);

        var document = SafeXml.Load(stream, bytes.Length);

        Assert.Equal("root", document.Root?.Name.LocalName);
        Assert.False(stream.IsDisposed);

        stream.Dispose();
    }

    private static MemoryStream CreateStream(string xml) => new(Encoding.UTF8.GetBytes(xml));

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        internal NonSeekableReadStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes);
        }

        internal bool IsDisposed { get; private set; }

        public override bool CanRead => !IsDisposed;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                IsDisposed = true;
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
