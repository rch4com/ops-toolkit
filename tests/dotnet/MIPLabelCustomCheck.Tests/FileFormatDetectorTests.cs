using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class FileFormatDetectorTests
{
    public static TheoryData<byte[], object> Signatures => new()
    {
        { ".pfile"u8.ToArray(), FileFormat.GenericPFile },
        { "%PDF-1.7"u8.ToArray(), FileFormat.Pdf },
        { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, FileFormat.Png },
        { new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, FileFormat.Jpeg },
        { new byte[] { 0x49, 0x49, 0x2A, 0x00 }, FileFormat.Tiff },
        { new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, FileFormat.Tiff },
        { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, FileFormat.Zip },
        { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }, FileFormat.CompoundFile }
    };

    [Theory]
    [MemberData(nameof(Signatures))]
    public void Detect_recognizes_magic(byte[] bytes, object expected)
    {
        using var stream = new MemoryStream(bytes);

        Assert.Equal(expected, FileFormatDetector.Detect(stream));
    }

    [Fact]
    public void Detect_finds_pdf_header_within_first_1024_bytes()
    {
        using var stream = new MemoryStream("comment\r\n%PDF-1.7"u8.ToArray());

        Assert.Equal(FileFormat.Pdf, FileFormatDetector.Detect(stream));
    }

    // Polyglot / cross-format confusion: an offset-0 container signature (Zip/CFB/PNG/PFile)
    // must win over a "%PDF-" marker that also happens to appear in the first 1 KiB, because
    // Detect checks those signatures before searching for the PDF header. These lock that
    // ordering so a future reshuffle of the checks in FileFormatDetector.Detect gets caught.
    public static TheoryData<byte[], object> PolyglotSignatures => new()
    {
        { BuildPolyglot([0x50, 0x4B, 0x03, 0x04]), FileFormat.Zip },
        { BuildPolyglot([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]), FileFormat.CompoundFile },
        { BuildPolyglot([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), FileFormat.Png },
        { BuildPolyglot(".pfile"u8.ToArray()), FileFormat.GenericPFile },
        { BuildPolyglot([0xFF, 0xD8, 0xFF, 0xE1]), FileFormat.Jpeg },
        { BuildPolyglot([0x49, 0x49, 0x2A, 0x00]), FileFormat.Tiff }
    };

    [Theory]
    [MemberData(nameof(PolyglotSignatures))]
    public void Detect_prefers_offset_zero_signature_over_embedded_pdf_header(byte[] bytes, object expected)
    {
        using var stream = new MemoryStream(bytes);

        Assert.Equal(expected, FileFormatDetector.Detect(stream));
    }

    private static byte[] BuildPolyglot(byte[] magic)
    {
        var body = new byte[64];
        magic.CopyTo(body, 0);
        "%PDF-1.7"u8.CopyTo(body.AsSpan(magic.Length));
        return body;
    }

    [Fact]
    public void Detect_restores_non_zero_input_position()
    {
        using var stream = new MemoryStream(".pfile content"u8.ToArray()) { Position = 4 };

        var result = FileFormatDetector.Detect(stream);

        Assert.Equal(FileFormat.GenericPFile, result);
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void Detect_does_not_find_pdf_header_beginning_after_byte_1023()
    {
        var bytes = new byte[1029];
        "%PDF-"u8.CopyTo(bytes.AsSpan(1024));
        using var stream = new MemoryStream(bytes);

        Assert.Equal(FileFormat.Unknown, FileFormatDetector.Detect(stream));
    }

    [Fact]
    public void Detect_handles_partial_reads_without_reading_past_signature_buffer()
    {
        var bytes = new byte[2048];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(bytes, 0);
        using var stream = new ChunkedReadStream(bytes, maxChunkSize: 3) { Position = 17 };

        var result = FileFormatDetector.Detect(stream);

        Assert.Equal(FileFormat.CompoundFile, result);
        Assert.InRange(stream.LargestReadRequest, 1, 1024);
        Assert.InRange(stream.LargestReadResult, 1, 3);
        Assert.Equal(8, stream.TotalBytesRead);
        Assert.Equal(17, stream.Position);
    }

    private sealed class ChunkedReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _maxChunkSize;

        internal ChunkedReadStream(byte[] bytes, int maxChunkSize)
        {
            _inner = new MemoryStream(bytes);
            _maxChunkSize = maxChunkSize;
        }

        internal int LargestReadRequest { get; private set; }

        internal int LargestReadResult { get; private set; }

        internal int TotalBytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            LargestReadRequest = Math.Max(LargestReadRequest, count);
            var read = _inner.Read(buffer, offset, Math.Min(count, _maxChunkSize));
            RecordRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            LargestReadRequest = Math.Max(LargestReadRequest, buffer.Length);
            var read = _inner.Read(buffer[..Math.Min(buffer.Length, _maxChunkSize)]);
            RecordRead(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void RecordRead(int read)
        {
            LargestReadResult = Math.Max(LargestReadResult, read);
            TotalBytesRead += read;
        }
    }
}
