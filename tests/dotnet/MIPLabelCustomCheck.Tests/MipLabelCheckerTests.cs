using System.Buffers.Binary;
using System.Text;
using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class MipLabelCheckerTests
{
    [Fact]
    public void Check_pfile_returns_protected_unknown_and_restores_position()
    {
        var bytes = new byte[300];
        ".pfile"u8.CopyTo(bytes);
        using var stream = new MemoryStream(bytes) { Position = 17 };

        var result = new MipLabelChecker().Check(stream, "anything.bin");

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.GenericPFile, result.Protection);
        Assert.False(result.IsLabeled);
        Assert.Empty(result.LabelIds);
        Assert.Equal(17, stream.Position);
    }

    [Fact]
    public async Task CheckAsync_path_canceled_before_start_throws_operation_canceled_exception()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Record.ExceptionAsync(() => new MipLabelChecker().CheckAsync(
            FixturePaths.Get(".png"),
            cancellationToken: cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    public async Task CheckAsync_stream_canceled_before_start_throws_operation_canceled_exception_and_keeps_position()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = new MemoryStream("unknown"u8.ToArray()) { Position = 3 };

        var exception = await Record.ExceptionAsync(() => new MipLabelChecker().CheckAsync(
            stream,
            cancellationToken: cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public async Task CheckAsync_path_returns_same_result_as_sync_check()
    {
        var checker = new MipLabelChecker();
        var path = FixturePaths.Get(".png");

        var expected = checker.Check(path, LabelCheckOptions.WithDetails);
        var actual = await checker.CheckAsync(path, LabelCheckOptions.WithDetails, CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CheckAsync_stream_restores_original_position_and_does_not_dispose_caller_stream()
    {
        var bytes = new byte[300];
        ".pfile"u8.CopyTo(bytes);
        using var stream = new MemoryStream(bytes) { Position = 17 };

        var result = await new MipLabelChecker().CheckAsync(stream, "anything.bin");

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.GenericPFile, result.Protection);
        Assert.Equal(17, stream.Position);
        Assert.True(stream.CanRead);
        stream.Position = 0;
        Assert.True(stream.ReadByte() >= 0);
    }

    [Fact]
    public void Check_unknown_signature_returns_unsupported() =>
        Assert.Equal(
            LabelStatus.Unsupported,
            new MipLabelChecker().Check(new MemoryStream("unknown"u8.ToArray())).Status);

    [Fact]
    public void Check_null_path_throws_argument_exception() =>
        Assert.Throws<ArgumentNullException>(() => new MipLabelChecker().Check((string)null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Check_empty_or_whitespace_path_throws_argument_exception(string path) =>
        Assert.Throws<ArgumentException>(() => new MipLabelChecker().Check(path));

    [Fact]
    public void Check_missing_file_throws_file_not_found_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mip-label-missing-{Guid.NewGuid():N}.bin");

        Assert.Throws<FileNotFoundException>(() => new MipLabelChecker().Check(path));
    }

    [Fact]
    public void Check_unreadable_stream_throws_argument_exception()
    {
        using var stream = new UnreadableStream();

        Assert.Throws<ArgumentException>(() => new MipLabelChecker().Check(stream));
    }

    [Fact]
    public void Check_non_seekable_stream_throws_not_supported_exception()
    {
        using var stream = new NonSeekableStream(new byte[] { 1, 2, 3 });

        Assert.Throws<NotSupportedException>(() => new MipLabelChecker().Check(stream));
    }

    [Fact]
    public void Check_does_not_dispose_caller_stream()
    {
        using var stream = new MemoryStream("unknown"u8.ToArray());

        new MipLabelChecker().Check(stream);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);

        stream.Position = 0;
        Assert.True(stream.ReadByte() >= 0);
    }

    [Fact]
    public void Check_restores_original_position_after_parser_exception()
    {
        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var expected = new InvalidOperationException("simulated parser failure");
        using var stream = new ThrowingAfterDetectionStream(pngSignature, expected)
        {
            Position = 3
        };

        var actual = Record.Exception(() =>
            new MipLabelChecker().Check(stream, "sample.png"));

        Assert.Same(expected, actual);
        Assert.Equal(3, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Check_path_succeeds_when_file_is_open_for_writing_with_shared_access()
    {
        var path = CreateTempFixtureCopy(".png");
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                var result = new MipLabelChecker().Check(path);

                Assert.Equal(LabelStatus.Labeled, result.Status);
                Assert.Single(result.LabelIds);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [WindowsOnlyFact]
    public void Check_path_throws_file_locked_when_file_is_open_exclusively()
    {
        var path = CreateTempFixtureCopy(".png");
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var exception = Assert.Throws<MipLabelCheckException>(
                    () => new MipLabelChecker().Check(path));

                Assert.Equal(MipLabelCheckErrorReason.FileLocked, exception.Reason);
                Assert.Equal(path, exception.FilePath);
                Assert.IsAssignableFrom<IOException>(exception.InnerException);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Same_instance_is_thread_safe_for_independent_files()
    {
        var checker = new MipLabelChecker();
        var paths = Enumerable.Range(0, 25)
            .SelectMany(_ => new[] { ".doc", ".docx", ".pdf", ".png", ".ppt", ".pptx", ".xls", ".xlsx" })
            .Select(FixturePaths.Get)
            .ToArray();

        Parallel.ForEach(paths, path =>
        {
            var result = checker.Check(path);
            Assert.Equal(LabelStatus.Labeled, result.Status);
            Assert.Single(result.LabelIds);
        });
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(33, true)]
    [InlineData(2, false)]
    [InlineData(unchecked((int)0x80070020), true)]
    public void IsSharingViolation_classifies_win32_error_by_low_16_bits(
        int hResult,
        bool expected) =>
        Assert.Equal(expected, MipLabelChecker.IsSharingViolation(hResult));

    private static string CreateTempFixtureCopy(string extension)
    {
        var sourceBytes = File.ReadAllBytes(FixturePaths.Get(extension));
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"mip-label-check-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(tempPath, sourceBytes);
        return tempPath;
    }

    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        internal NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingAfterDetectionStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly Exception _exception;
        private int _readsFromStart;

        internal ThrowingAfterDetectionStream(byte[] data, Exception exception)
        {
            _inner = new MemoryStream(data);
            _exception = exception;
        }

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
            ThrowOnSecondReadFromStart();
            return _inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            ThrowOnSecondReadFromStart();
            return _inner.Read(buffer);
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ThrowOnSecondReadFromStart()
        {
            if (_inner.Position == 0 && ++_readsFromStart == 2)
            {
                throw _exception;
            }
        }
    }
}

public sealed class JpegTiffLabelReaderTests
{
    private static readonly Guid FirstLabelId =
        Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");

    [Fact]
    public void Check_dispatches_jpeg_app1_xmp_and_restores_position()
    {
        using var stream = new JpegFixture()
            .AddApp1Xmp(AttributeXml(FirstLabelId))
            .AddStartOfScan([0xFF, 0x00, 0x01, 0x02])
            .Create();
        stream.Position = 3;

        var result = new MipLabelChecker().Check(stream, "sample.jpg");

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
        Assert.Equal(new[] { LabelSource.Xmp }, result.Sources);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void Jpeg_read_finds_uncompressed_app1_xmp()
    {
        using var stream = new JpegFixture().AddApp1Xmp(AttributeXml(FirstLabelId)).Create();

        var result = JpegLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
        Assert.Equal(new[] { LabelSource.Xmp }, result.Sources);
    }

    [Fact]
    public void Jpeg_read_skips_compressed_scan_data_without_decoding_it()
    {
        using var stream = new JpegFixture()
            .AddStartOfScan(Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId)))
            .Create();

        var result = JpegLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Unlabeled, result.Status);
    }

    [Fact]
    public void Jpeg_read_returns_invalid_for_truncated_segment()
    {
        using var stream = new JpegFixture()
            .AddRawSegment(0xE1, [1, 2, 3], declaredPayloadLength: 100)
            .WithoutEoi()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, JpegLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Jpeg_read_returns_invalid_when_aggregate_xmp_exceeds_limit()
    {
        var fixture = new JpegFixture();
        var xml = $"<root>{new string(' ', 65_000 - 13)}</root>";
        for (var index = 0; index < 65; index++)
        {
            fixture.AddApp1Xmp(xml);
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, JpegLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Check_dispatches_little_endian_tiff_xmp_and_restores_position()
    {
        using var stream = TiffFixture.Create(AttributeXml(FirstLabelId), littleEndian: true);
        stream.Position = 2;

        var result = new MipLabelChecker().Check(stream, "sample.tif");

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
        Assert.Equal(new[] { LabelSource.Xmp }, result.Sources);
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void Tiff_read_finds_big_endian_xmp()
    {
        using var stream = TiffFixture.Create(AttributeXml(FirstLabelId), littleEndian: false);

        var result = TiffLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Fact]
    public void Tiff_read_returns_unlabeled_for_inactive_xmp_metadata()
    {
        using var stream = TiffFixture.Create(AttributeXml(FirstLabelId, "false"), littleEndian: true);

        Assert.Equal(LabelStatus.Unlabeled, TiffLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Tiff_read_returns_invalid_for_truncated_ifd_entry()
    {
        using var stream = TiffFixture.CreateTruncatedIfd();

        Assert.Equal(LabelStatus.InvalidFile, TiffLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Tiff_read_returns_invalid_for_oversized_xmp_tag()
    {
        using var stream = TiffFixture.CreateOversizedXmpTag();

        Assert.Equal(LabelStatus.InvalidFile, TiffLabelReader.Read(stream).Status);
    }

    private static string AttributeXml(Guid labelId, string value = "true") => $$"""
        <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:msip">
          <rdf:Description msip:MSIP_Label_{{labelId:D}}_Enabled="{{value}}" />
        </rdf:RDF>
        """;

    private sealed class JpegFixture
    {
        private static readonly byte[] XmpIdentifier =
            "http://ns.adobe.com/xap/1.0/\0"u8.ToArray();

        private readonly List<byte[]> _parts = [];
        private bool _includeEoi = true;

        internal JpegFixture AddApp1Xmp(string xml)
        {
            var payload = XmpIdentifier.Concat(Encoding.UTF8.GetBytes(xml)).ToArray();
            return AddRawSegment(0xE1, payload);
        }

        internal JpegFixture AddRawSegment(
            byte marker,
            byte[] payload,
            int? declaredPayloadLength = null)
        {
            using var stream = new MemoryStream();
            stream.WriteByte(0xFF);
            stream.WriteByte(marker);
            Span<byte> length = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(
                length,
                checked((ushort)((declaredPayloadLength ?? payload.Length) + 2)));
            stream.Write(length);
            stream.Write(payload);
            _parts.Add(stream.ToArray());
            return this;
        }

        internal JpegFixture AddStartOfScan(byte[] scanData)
        {
            _parts.Add([0xFF, 0xDA, 0x00, 0x02, .. scanData]);
            _includeEoi = false;
            return this;
        }

        internal JpegFixture WithoutEoi()
        {
            _includeEoi = false;
            return this;
        }

        internal MemoryStream Create()
        {
            using var stream = new MemoryStream();
            stream.WriteByte(0xFF);
            stream.WriteByte(0xD8);
            foreach (var part in _parts)
            {
                stream.Write(part);
            }

            if (_includeEoi)
            {
                stream.WriteByte(0xFF);
                stream.WriteByte(0xD9);
            }

            return new MemoryStream(stream.ToArray());
        }
    }

    private static class TiffFixture
    {
        internal static MemoryStream Create(string xml, bool littleEndian)
        {
            var xmp = Encoding.UTF8.GetBytes(xml);
            using var stream = new MemoryStream();
            WriteHeader(stream, littleEndian, firstIfdOffset: 8);
            WriteUInt16(stream, 1, littleEndian);
            WriteUInt16(stream, 700, littleEndian);
            WriteUInt16(stream, 1, littleEndian);
            WriteUInt32(stream, checked((uint)xmp.Length), littleEndian);
            WriteUInt32(stream, 26, littleEndian);
            WriteUInt32(stream, 0, littleEndian);
            stream.Write(xmp);
            return new MemoryStream(stream.ToArray());
        }

        internal static MemoryStream CreateTruncatedIfd()
        {
            using var stream = new MemoryStream();
            WriteHeader(stream, littleEndian: true, firstIfdOffset: 8);
            WriteUInt16(stream, 1, littleEndian: true);
            stream.WriteByte(0xBC);
            return new MemoryStream(stream.ToArray());
        }

        internal static MemoryStream CreateOversizedXmpTag()
        {
            using var stream = new MemoryStream();
            WriteHeader(stream, littleEndian: true, firstIfdOffset: 8);
            WriteUInt16(stream, 1, littleEndian: true);
            WriteUInt16(stream, 700, littleEndian: true);
            WriteUInt16(stream, 1, littleEndian: true);
            WriteUInt32(stream, PngLabelReader.MaxDecodedXmpBytes + 1u, littleEndian: true);
            WriteUInt32(stream, 26, littleEndian: true);
            WriteUInt32(stream, 0, littleEndian: true);
            return new MemoryStream(stream.ToArray());
        }

        private static void WriteHeader(Stream stream, bool littleEndian, uint firstIfdOffset)
        {
            stream.Write(littleEndian ? "II"u8 : "MM"u8);
            WriteUInt16(stream, 42, littleEndian);
            WriteUInt32(stream, firstIfdOffset, littleEndian);
        }

        private static void WriteUInt16(Stream stream, ushort value, bool littleEndian)
        {
            Span<byte> buffer = stackalloc byte[2];
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            }

            stream.Write(buffer);
        }

        private static void WriteUInt32(Stream stream, uint value, bool littleEndian)
        {
            Span<byte> buffer = stackalloc byte[4];
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            }

            stream.Write(buffer);
        }
    }
}

/// <summary>
/// An xunit <see cref="FactAttribute"/> that skips the test at discovery time on non-Windows
/// platforms, instead of the test running and vacuously passing after an early return.
/// </summary>
internal sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows-only sharing semantics.";
        }
    }
}
