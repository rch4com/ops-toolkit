using MIPLabelCustomCheck.Internal.CompoundFile;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class CompoundFileReaderTests
{
    [Theory]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    public void Finds_document_summary_information(string extension)
    {
        using var stream = File.OpenRead(FixturePaths.Get(extension));
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.True(file.TryReadStream("\u0005DocumentSummaryInformation", out var bytes));
        Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData(".doc", "WordDocument")]
    [InlineData(".xls", "Workbook")]
    [InlineData(".ppt", "PowerPoint Document")]
    public void Finds_representative_regular_stream(string extension, string streamName)
    {
        using var stream = File.OpenRead(FixturePaths.Get(extension));
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.True(file.TryReadStream(streamName, out var bytes));
        Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Reads_nested_mini_regular_and_zero_length_streams(int majorVersion)
    {
        var fixture = majorVersion == 3
            ? CompoundFileFixture.CreateV3()
            : CompoundFileFixture.CreateV4();
        using var stream = new MemoryStream(fixture.Bytes);
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.True(file.TryReadStream("Nested/\u0006DataSpaces", out var mini));
        Assert.Equal(fixture.MiniPayload, mini);
        Assert.True(file.TryReadStream("Regular", out var regular));
        Assert.Equal(fixture.RegularPayload, regular);
        Assert.True(file.TryReadStream("Nested/Zero", out var zero));
        Assert.Empty(zero);
    }

    [Fact]
    public void Reads_a_file_whose_fat_sector_ids_continue_in_the_difat_chain()
    {
        var fixture = CompoundFileFixture.CreateV3WithDifat();
        using var stream = new MemoryStream(fixture.Bytes);
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.True(file.TryReadStream("Regular", out var bytes));
        Assert.Equal(fixture.RegularPayload, bytes);
    }

    [Fact]
    public void Lookup_is_exact_ordinal_and_non_stream_paths_are_not_returned()
    {
        var fixture = CompoundFileFixture.CreateV3();
        using var stream = new MemoryStream(fixture.Bytes);
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.False(file.TryReadStream("regular", out _));
        Assert.False(file.TryReadStream("Nested", out _));
        Assert.False(file.TryReadStream("Missing", out _));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Supports_partial_reads_from_a_seekable_stream(int majorVersion)
    {
        var fixture = majorVersion == 3
            ? CompoundFileFixture.CreateV3()
            : CompoundFileFixture.CreateV4();
        using var stream = new ChunkedReadStream(fixture.Bytes, maxChunkSize: 3);
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.True(file.TryReadStream("Nested/\u0006DataSpaces", out var bytes));
        Assert.Equal(fixture.MiniPayload, bytes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Dispose_honors_leave_open(bool leaveOpen, bool expectedDisposed)
    {
        var fixture = CompoundFileFixture.CreateV3();
        var stream = new TrackingStream(fixture.Bytes);
        var file = new CompoundFileReader(stream, leaveOpen);

        file.Dispose();

        Assert.Equal(expectedDisposed, stream.IsDisposed);
        stream.Dispose();
    }

    [Fact]
    public void Reading_a_small_mini_stream_does_not_read_unrelated_regular_payload()
    {
        var fixture = CompoundFileFixture.CreateV3();
        using var stream = new TrackingStream(fixture.Bytes);
        using var file = new CompoundFileReader(stream, leaveOpen: true);

        Assert.True(file.TryReadStream("Nested/\u0006DataSpaces", out _));

        Assert.False(stream.WasRead(
            fixture.SectorOffset(fixture.RegularFirstSectorId),
            fixture.RegularPayload.Length));
        Assert.False(stream.WasRead(
            fixture.SectorOffset(fixture.RootMiniStreamSectorId) + fixture.MiniPayload.Length,
            fixture.SectorSize - fixture.MiniPayload.Length));
    }

    [Fact]
    public void Rejects_bad_signature()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        bytes[0] ^= 0xFF;
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_bad_byte_order()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetHeaderUInt16(bytes, 28, 0xFEFF);
        AssertConstructorInvalid(bytes);
    }

    [Theory]
    [InlineData(2, 9)]
    [InlineData(3, 12)]
    [InlineData(4, 9)]
    public void Rejects_unsupported_major_version_or_sector_shift(ushort major, ushort shift)
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetHeaderUInt16(bytes, 26, major);
        fixture.SetHeaderUInt16(bytes, 30, shift);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_fat_sector_outside_file()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetHeaderUInt32(bytes, 76, checked((uint)fixture.PhysicalSectorCount + 1));
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_wrong_fat_marker()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetFatEntry(bytes, fixture.FatSectorId, CompoundFileFixture.EndOfChain);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_difat_cycle()
    {
        var fixture = CompoundFileFixture.CreateV3WithDifat();
        var bytes = fixture.CloneBytes();
        fixture.SetHeaderUInt32(bytes, 72, 2);
        fixture.SetHeaderUInt32(
            bytes,
            fixture.SectorOffset(fixture.DifatSectorId) + fixture.SectorSize - 4,
            fixture.DifatSectorId);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_difat_count_mismatch()
    {
        var fixture = CompoundFileFixture.CreateV3WithDifat();
        var bytes = fixture.CloneBytes();
        fixture.SetHeaderUInt32(bytes, 72, 2);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_wrong_difat_marker()
    {
        var fixture = CompoundFileFixture.CreateV3WithDifat();
        var bytes = fixture.CloneBytes();
        fixture.SetFatEntry(bytes, fixture.DifatSectorId, CompoundFileFixture.EndOfChain);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_directory_chain_cycle()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetFatEntry(bytes, fixture.LastDirectorySectorId, fixture.FirstDirectorySectorId);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_malformed_directory_name()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetDirectoryNameLength(bytes, CompoundFileFixture.MiniEntryId, 3);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_invalid_directory_type()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetDirectoryType(bytes, CompoundFileFixture.MiniEntryId, 3);
        AssertConstructorInvalid(bytes);
    }

    [Theory]
    [InlineData(3, 1L)]
    [InlineData(3, 4294967296L)]
    [InlineData(4, 1L)]
    public void Rejects_storage_with_nonzero_stream_size(int majorVersion, long streamSize)
    {
        var fixture = majorVersion == 3
            ? CompoundFileFixture.CreateV3()
            : CompoundFileFixture.CreateV4();
        var bytes = fixture.CloneBytes();
        fixture.SetDirectorySize(bytes, 1, checked((ulong)streamSize));

        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_sibling_cycle()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetDirectoryRightSibling(bytes, 1, 1);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_child_cycle()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetDirectoryChild(bytes, 1, 1);
        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_storage_nesting_beyond_the_depth_limit()
    {
        var bytes = CompoundFileFixture.CreateV3StorageChain(storageCount: 257);

        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_sibling_tree_beyond_the_depth_limit()
    {
        var bytes = CompoundFileFixture.CreateV3SiblingChain(streamCount: 257);

        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_directory_paths_beyond_the_aggregate_character_limit()
    {
        var bytes = CompoundFileFixture.CreateV3WideNestedTree(streamCount: 5000);

        AssertConstructorInvalid(bytes);
    }

    [Fact]
    public void Rejects_mini_fat_cycle()
    {
        AssertReadInvalid(
            CompoundFileFixture.CreateV3(),
            "Nested/\u0006DataSpaces",
            (fixture, bytes) => fixture.SetMiniFatEntry(bytes, 0, 0));
    }

    [Fact]
    public void Rejects_mini_fat_sector_outside_root_stream()
    {
        AssertReadInvalid(
            CompoundFileFixture.CreateV3(),
            "Nested/\u0006DataSpaces",
            (fixture, bytes) => fixture.SetMiniFatEntry(bytes, 0, 999));
    }

    [Fact]
    public void Rejects_regular_chain_cycle()
    {
        AssertReadInvalid(
            CompoundFileFixture.CreateV3(),
            "Regular",
            (fixture, bytes) =>
                fixture.SetFatEntry(bytes, fixture.RegularFirstSectorId, fixture.RegularFirstSectorId));
    }

    [Fact]
    public void Rejects_premature_regular_chain()
    {
        AssertReadInvalid(
            CompoundFileFixture.CreateV3(),
            "Regular",
            (fixture, bytes) =>
                fixture.SetFatEntry(bytes, fixture.RegularFirstSectorId, CompoundFileFixture.EndOfChain));
    }

    [Fact]
    public void Rejects_overlong_regular_chain()
    {
        AssertReadInvalid(
            CompoundFileFixture.CreateV3(),
            "Regular",
            (fixture, bytes) =>
                fixture.SetDirectorySize(bytes, CompoundFileFixture.RegularEntryId, 4096));
    }

    [Fact]
    public void Rejects_stream_larger_than_return_limit_without_allocating_it()
    {
        AssertReadInvalid(
            CompoundFileFixture.CreateV3(),
            "Regular",
            (fixture, bytes) => fixture.SetDirectorySize(
                bytes,
                CompoundFileFixture.RegularEntryId,
                (4u * 1024 * 1024) + 1));
    }

    [Fact]
    public void Rejects_truncated_or_misaligned_file()
    {
        var fixture = CompoundFileFixture.CreateV4();
        AssertConstructorInvalid(fixture.Bytes[..^1]);
    }

    [Fact]
    public void Rejects_incorrect_v4_directory_sector_count()
    {
        var fixture = CompoundFileFixture.CreateV4();
        var bytes = fixture.CloneBytes();
        fixture.SetHeaderUInt32(bytes, 40, 0);
        AssertConstructorInvalid(bytes);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(4095)]
    public void Rejects_nonzero_v4_header_padding(int offset)
    {
        var fixture = CompoundFileFixture.CreateV4();
        var bytes = fixture.CloneBytes();
        bytes[offset] = 1;

        AssertConstructorInvalid(bytes);
    }

    private static void AssertConstructorInvalid(byte[] bytes) =>
        Assert.Throws<InvalidDataException>(() =>
        {
            using var stream = new MemoryStream(bytes);
            using var file = new CompoundFileReader(stream, leaveOpen: true);
        });

    private static void AssertReadInvalid(
        CompoundFileFixture fixture,
        string path,
        Action<CompoundFileFixture, byte[]> mutate)
    {
        var bytes = fixture.CloneBytes();
        mutate(fixture, bytes);

        Assert.Throws<InvalidDataException>(() =>
        {
            using var stream = new MemoryStream(bytes);
            using var file = new CompoundFileReader(stream, leaveOpen: true);
            file.TryReadStream(path, out _);
        });
    }

    private class TrackingStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly List<(long Start, long End)> _reads = [];

        internal TrackingStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        internal bool IsDisposed { get; private set; }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

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
            var start = Position;
            var read = _inner.Read(buffer, offset, count);
            Record(start, read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var start = Position;
            var read = _inner.Read(buffer);
            Record(start, read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        internal bool WasRead(long offset, long length)
        {
            var end = checked(offset + length);
            return _reads.Any(read => read.Start < end && read.End > offset);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Record(long start, int read)
        {
            if (read > 0)
            {
                _reads.Add((start, start + read));
            }
        }
    }

    private sealed class ChunkedReadStream : TrackingStream
    {
        private readonly int _maxChunkSize;

        internal ChunkedReadStream(byte[] bytes, int maxChunkSize)
            : base(bytes) => _maxChunkSize = maxChunkSize;

        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, Math.Min(count, _maxChunkSize));

        public override int Read(Span<byte> buffer) =>
            base.Read(buffer[..Math.Min(buffer.Length, _maxChunkSize)]);
    }
}
