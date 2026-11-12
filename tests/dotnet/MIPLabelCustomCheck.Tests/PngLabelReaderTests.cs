using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using MIPLabelCustomCheck.Internal;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class PngLabelReaderTests
{
    private static readonly Guid RealLabelId =
        Guid.Parse("e1bdcc70-4bee-4888-a93a-4203339b523a");

    private static readonly Guid FirstLabelId =
        Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");

    private static readonly Guid SecondLabelId =
        Guid.Parse("e19cdb69-0515-4f99-a455-60417f2e080d");

    [Fact]
    public void Read_real_png_returns_expected_unprotected_label()
    {
        using var stream = File.OpenRead(FixturePaths.Get(".png"));

        var result = PngLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { RealLabelId }, result.LabelIds);
        Assert.Equal(ProtectionType.None, result.Protection);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Check_dispatches_real_png_and_restores_open_stream()
    {
        using var stream = File.OpenRead(FixturePaths.Get(".png"));
        stream.Position = 7;

        var result = new MipLabelChecker().Check(stream, "sample.png");

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { RealLabelId }, result.LabelIds);
        Assert.Equal(ProtectionType.None, result.Protection);
        Assert.True(stream.CanRead);
        Assert.Equal(7, stream.Position);
    }

    [Fact]
    public void Read_reports_xmp_source_for_xmp_label()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(AttributeXml(FirstLabelId)))
            .Create();

        var result = PngLabelReader.Read(stream);

        Assert.Equal(new[] { LabelSource.Xmp }, result.Sources);
    }

    [Fact]
    public void Read_real_png_reports_xmp_source()
    {
        using var stream = File.OpenRead(FixturePaths.Get(".png"));

        var result = PngLabelReader.Read(stream);

        Assert.Equal(new[] { LabelSource.Xmp }, result.Sources);
    }

    [Fact]
    public void Read_finds_uncompressed_xmp_attribute()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(AttributeXml(FirstLabelId)))
            .Create();

        var result = PngLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_finds_zlib_compressed_xmp_attribute()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                AttributeXml(FirstLabelId),
                CompressionFlag: 1))
            .Create();

        var result = PngLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_finds_xmp_leaf_element()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(ElementXml(FirstLabelId)))
            .Create();

        Assert.Equal(new[] { FirstLabelId }, PngLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_uses_xpacket_end_processing_instruction_as_packet_boundary()
    {
        var xml =
            "<?xpacket begin=\"\uFEFF\" id=\"test\"?>\n" +
            AttributeXml(FirstLabelId) +
            "\n<?xpacket end=\"r\"?>=\"r\"?>";
        using var stream = new PngFixture().AddITxt(new PngITxtOptions(xml)).Create();

        var result = PngLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_returns_invalid_for_arbitrary_bytes_after_xpacket_end()
    {
        var xml =
            "<?xpacket begin=\"\uFEFF\" id=\"test\"?>\n" +
            AttributeXml(FirstLabelId) +
            "\n<?xpacket end=\"r\"?><broken";
        using var stream = new PngFixture().AddITxt(new PngITxtOptions(xml)).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_does_not_treat_xpacket_marker_inside_comment_as_packet_end()
    {
        var body = AttributeXml(FirstLabelId).Replace(
            "</rdf:RDF>",
            "<!-- <?xpacket end=\"r\"?> -->\n</rdf:RDF>",
            StringComparison.Ordinal);
        var xml =
            "<?xpacket begin=\"\uFEFF\" id=\"test\"?>\n" +
            body +
            "\n<?xpacket end=\"r\"?>";
        using var stream = new PngFixture().AddITxt(new PngITxtOptions(xml)).Create();

        Assert.Equal(new[] { FirstLabelId }, PngLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_preserves_first_xmp_order_and_deduplicates_across_chunks()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(AttributeXml(FirstLabelId)))
            .AddITxt(new PngITxtOptions(ElementXml(SecondLabelId)))
            .AddITxt(new PngITxtOptions(ElementXml(FirstLabelId)))
            .Create();

        Assert.Equal(
            new[] { FirstLabelId, SecondLabelId },
            PngLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_returns_unlabeled_for_inactive_xmp_metadata()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(AttributeXml(FirstLabelId, "false")))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unlabeled_for_valid_png_without_xmp()
    {
        using var stream = new PngFixture().Create();

        Assert.Equal(LabelStatus.Unlabeled, PngLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    public void Read_returns_invalid_for_invalid_itxt_compression_fields(
        byte flag,
        byte method)
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                AttributeXml(FirstLabelId),
                CompressionFlag: flag,
                CompressionMethod: method))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_malformed_itxt_null_fields()
    {
        var data = Encoding.Latin1.GetBytes(PngFixture.XmpKeyword)
            .Concat(new byte[] { 0, 0, 0 })
            .Concat("language-without-terminator"u8.ToArray())
            .ToArray();
        using var stream = new PngFixture().AddRawITxt(data).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_xmp_keyword_has_no_null_terminator()
    {
        var data = Encoding.Latin1.GetBytes(PngFixture.XmpKeyword);
        using var stream = new PngFixture().AddRawITxt(data).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData("language")]
    [InlineData("translated")]
    [InlineData("text")]
    public void Read_returns_invalid_for_non_ascii_or_invalid_utf8_itxt_fields(
        string field)
    {
        var options = field switch
        {
            "language" => new PngITxtOptions(LanguageTagBytes: [0x80]),
            "translated" => new PngITxtOptions(TranslatedKeywordBytes: [0xC3, 0x28]),
            "text" => new PngITxtOptions(TextBytes: [0xC3, 0x28]),
            _ => throw new InvalidOperationException()
        };
        using var stream = new PngFixture().AddITxt(options).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_corrupt_inspected_xmp_crc()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                AttributeXml(FirstLabelId),
                CorruptCrc: true))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_malformed_xmp_xml()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions("<rdf:RDF"))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_malformed_active_label_property()
    {
        const string xml =
            "<root xmlns:msip=\"urn:msip\" MSIP_Label_not-a-guid_Enabled=\"true\" />";
        using var stream = new PngFixture().AddITxt(new PngITxtOptions(xml)).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_bad_zlib_payload()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: [1, 2, 3, 4],
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_bytes_after_zlib_stream()
    {
        var compressed = CompressZlib(Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId)));
        var storedText = compressed.Concat(new byte[] { 0xAA, 0xBB, 0xCC }).ToArray();
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: storedText,
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_concatenated_zlib_streams()
    {
        var compressed = CompressZlib(Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId)));
        var storedText = compressed.Concat(compressed).ToArray();
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: storedText,
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_concatenated_different_zlib_streams()
    {
        var first = CompressZlib(Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId)));
        var second = CompressZlib(Encoding.UTF8.GetBytes(AttributeXml(SecondLabelId)));
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: first.Concat(second).ToArray(),
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Read_returns_invalid_for_truncated_zlib_stream(int removedBytes)
    {
        var compressed = CompressZlib(Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId)));
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: compressed[..^removedBytes],
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData(CompressionLevel.Fastest)]
    [InlineData(CompressionLevel.Optimal)]
    public void Read_accepts_complete_zlib_streams_from_standard_compression_levels(
        CompressionLevel compressionLevel)
    {
        var compressed = CompressZlib(
            Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId)),
            compressionLevel);
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: compressed,
                CompressText: false))
            .Create();

        Assert.Equal(new[] { FirstLabelId }, PngLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_accepts_dynamic_block_with_singleton_distance_tree()
    {
        var expected = "<r/>"u8.ToArray();
        var compressed = DynamicDeflateFixture.Create(
            singletonDistanceTree: true,
            emitLengthWithoutDistance: false);
        Assert.Equal(expected, DecompressZlib(compressed));
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: compressed,
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_accepts_literal_only_dynamic_block_without_distance_codes()
    {
        var expected = "<r/>"u8.ToArray();
        var compressed = DynamicDeflateFixture.Create(
            singletonDistanceTree: false,
            emitLengthWithoutDistance: false);
        Assert.Equal(expected, DecompressZlib(compressed));
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                CompressionFlag: 1,
                TextBytes: compressed,
                CompressText: false))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Framing_validator_rejects_length_symbol_without_distance_codes()
    {
        var compressed = DynamicDeflateFixture.Create(
            singletonDistanceTree: false,
            emitLengthWithoutDistance: true);

        Assert.Throws<InvalidDataException>(() =>
            DeflateFramingValidator.Validate(compressed, "<r/>"u8, 1024));
    }

    [Fact]
    public void Read_returns_invalid_for_oversized_decompressed_xmp()
    {
        var xml = $"<root>{new string(' ', PngLabelReader.MaxDecodedXmpBytes + 1)}</root>";
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(xml, CompressionFlag: 1))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_accepts_near_limit_compressed_xmp()
    {
        const string prefix = "<root>";
        const string suffix = "</root>";
        var xml = prefix +
            new string(' ', PngLabelReader.MaxDecodedXmpBytes - prefix.Length - suffix.Length) +
            suffix;
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(xml, CompressionFlag: 1))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_aggregate_xmp_exceeds_limit()
    {
        var xml = $"<root>{new string(' ', PngLabelReader.MaxAggregateXmpBytes / 2)}</root>";
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(xml))
            .AddITxt(new PngITxtOptions(xml))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_too_many_xmp_chunks()
    {
        var fixture = new PngFixture();
        for (var index = 0; index <= PngLabelReader.MaxXmpChunkCount; index++)
        {
            fixture.AddITxt(new PngITxtOptions("<root />"));
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_seeks_over_idat_payload_without_reading_image_bytes()
    {
        var idat = Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId));
        var data = new PngFixture().WithIdat(idat).CreateData();
        using var stream = new ReadGuardStream(
            data.Bytes,
            data.IdatDataOffset!.Value,
            data.IdatDataLength);

        var result = PngLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Unlabeled, result.Status);
        Assert.False(stream.ReadGuardRange);
    }

    [Fact]
    public void Check_does_not_read_idat_payload_during_format_detection()
    {
        var data = new PngFixture().WithIdat(new byte[128]).CreateData();
        using var stream = new ReadGuardStream(
            data.Bytes,
            data.IdatDataOffset!.Value,
            data.IdatDataLength)
        {
            Position = 7
        };

        var result = new MipLabelChecker().Check(stream, "sample.png");

        Assert.Equal(LabelStatus.Unlabeled, result.Status);
        Assert.False(stream.ReadGuardRange);
        Assert.Equal(7, stream.Position);
    }

    [Fact]
    public void Read_ignores_non_xmp_itxt_without_validating_text_or_crc()
    {
        var labelText = Encoding.UTF8.GetBytes(AttributeXml(FirstLabelId));
        var oversizedBadText = new byte[PngLabelReader.MaxXmpChunkBytes + 1];
        labelText.CopyTo(oversizedBadText, 0);
        oversizedBadText[^1] = 0xFF;
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions(
                Keyword: "Comment",
                CompressionFlag: 7,
                CompressionMethod: 9,
                TextBytes: oversizedBadText,
                CorruptCrc: true))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, PngLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(100u)]
    [InlineData(uint.MaxValue)]
    public void Read_returns_invalid_when_declared_chunk_length_exceeds_file(uint length)
    {
        using var stream = new PngFixture()
            .AddChunk("tEXt", [1], declaredLength: length)
            .WithoutIend()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_rejects_png_chunk_length_with_high_bit_set_before_seeking()
    {
        var prefix = new PngFixture()
            .AddChunk("tEXt", [], declaredLength: 0x80000000)
            .WithoutIend()
            .Create()
            .ToArray();
        using var stream = new SparseLengthStream(
            prefix,
            virtualLength: prefix.Length + 0x80000000L);

        var result = PngLabelReader.Read(stream);

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
        Assert.False(stream.LargeSeekAttempted);
    }

    [Fact]
    public void Read_returns_invalid_for_bad_signature()
    {
        using var stream = new PngFixture().WithBadSignature().Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_ihdr_is_missing()
    {
        using var stream = new PngFixture().WithoutIhdr().Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_first_chunk_is_not_ihdr()
    {
        using var stream = new PngFixture()
            .WithoutIhdr()
            .AddChunk("tEXt", [])
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_ihdr_with_wrong_length()
    {
        using var stream = new PngFixture().WithIhdrLength(12).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_non_ascii_chunk_type()
    {
        using var stream = new PngFixture().AddChunk("A?CD", []).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_without_iend()
    {
        using var stream = new PngFixture().WithoutIend().Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_nonzero_iend_length()
    {
        using var stream = new PngFixture().WithIendLength(1).Create();

        Assert.Equal(LabelStatus.InvalidFile, PngLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_requires_seekable_stream()
    {
        var data = new PngFixture().CreateData().Bytes;
        using var stream = new NonSeekableReadStream(data);

        Assert.Throws<NotSupportedException>(() => PngLabelReader.Read(stream));
    }

    [Fact]
    public void Read_propagates_invalid_data_exception_from_caller_stream()
    {
        using var stream = new ThrowingInvalidDataReadStream(
            new PngFixture().CreateData().Bytes);

        var exception = Assert.Throws<InvalidDataException>(() => PngLabelReader.Read(stream));

        Assert.Equal("caller stream failure", exception.Message);
    }

    [Fact]
    public void Check_propagates_caller_xml_exception_and_restores_position() =>
        AssertCheckerPropagatesCallerException(
            new XmlException("caller XML failure"));

    [Fact]
    public void Check_propagates_caller_decoder_exception_and_restores_position() =>
        AssertCheckerPropagatesCallerException(
            new DecoderFallbackException("caller decoder failure"));

    [Fact]
    public void Crc32_matches_ieee_known_vector() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));

    private static string AttributeXml(Guid labelId, string value = "true") => $$"""
        <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:msip">
          <rdf:Description msip:MSIP_Label_{{labelId:D}}_Enabled="{{value}}" />
        </rdf:RDF>
        """;

    private static string ElementXml(Guid labelId) => $$"""
        <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:msip">
          <rdf:Description>
            <msip:MSIP_Label_{{labelId:D}}_Enabled>1</msip:MSIP_Label_{{labelId:D}}_Enabled>
          </rdf:Description>
        </rdf:RDF>
        """;

    private static byte[] CompressZlib(
        byte[] bytes,
        CompressionLevel compressionLevel = CompressionLevel.SmallestSize)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, compressionLevel, leaveOpen: true))
        {
            zlib.Write(bytes);
        }

        return output.ToArray();
    }

    private static byte[] DecompressZlib(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static void AssertCheckerPropagatesCallerException(Exception expected)
    {
        using var stream = new ThrowingAfterDetectionReadStream(
            new PngFixture().CreateData().Bytes,
            expected)
        {
            Position = 7
        };

        var actual = Record.Exception(() =>
            new MipLabelChecker().Check(stream, "sample.png"));

        Assert.Same(expected, actual);
        Assert.Equal(7, stream.Position);
        Assert.True(stream.CanRead);
    }

    private sealed class ReadGuardStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly long _guardStart;
        private readonly long _guardEnd;

        internal ReadGuardStream(byte[] data, int guardStart, int guardLength)
        {
            _inner = new MemoryStream(data);
            _guardStart = guardStart;
            _guardEnd = guardStart + guardLength;
        }

        internal bool ReadGuardRange { get; private set; }

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
            CheckRead(count);
            return _inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            CheckRead(buffer.Length);
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

        private void CheckRead(int count)
        {
            var readStart = _inner.Position;
            var readEnd = readStart + count;
            if (readStart < _guardEnd && readEnd > _guardStart)
            {
                ReadGuardRange = true;
                throw new InvalidOperationException("The IDAT payload must be skipped with Seek.");
            }
        }
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream _inner;

        internal NonSeekableReadStream(byte[] data) => _inner = new MemoryStream(data);

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
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
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

    private sealed class SparseLengthStream : Stream
    {
        private readonly byte[] _prefix;
        private readonly long _length;
        private long _position;

        internal SparseLengthStream(byte[] prefix, long virtualLength)
        {
            _prefix = prefix;
            _length = virtualLength;
        }

        internal bool LargeSeekAttempted { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var available = (int)Math.Min(buffer.Length, _length - _position);
            if (available <= 0)
            {
                return 0;
            }

            buffer[..available].Clear();
            if (_position < _prefix.Length)
            {
                var prefixBytes = (int)Math.Min(available, _prefix.Length - _position);
                _prefix.AsSpan((int)_position, prefixBytes).CopyTo(buffer);
            }

            _position += available;
            return available;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

            if (Math.Abs(target - _position) > 1024)
            {
                LargeSeekAttempted = true;
                throw new InvalidOperationException("A high-bit PNG length must not be sought.");
            }

            _position = target;
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingInvalidDataReadStream : Stream
    {
        private readonly MemoryStream _inner;

        internal ThrowingInvalidDataReadStream(byte[] data) => _inner = new MemoryStream(data);

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
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidDataException("caller stream failure");
        public override int Read(Span<byte> buffer) =>
            throw new InvalidDataException("caller stream failure");
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
    }

    private sealed class ThrowingAfterDetectionReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly Exception _exception;
        private int _readsFromStart;

        internal ThrowingAfterDetectionReadStream(byte[] data, Exception exception)
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

    private static class DynamicDeflateFixture
    {
        private static readonly int[] CodeLengthOrder =
            [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1];

        internal static byte[] Create(
            bool singletonDistanceTree,
            bool emitLengthWithoutDistance)
        {
            var literalLengthCount = emitLengthWithoutDistance ? 258 : 257;
            var literalLengths = new byte[literalLengthCount];
            if (emitLengthWithoutDistance)
            {
                literalLengths[47] = 2;
                literalLengths[60] = 2;
                literalLengths[62] = 3;
                literalLengths[114] = 3;
                literalLengths[256] = 3;
                literalLengths[257] = 3;
            }
            else
            {
                literalLengths[47] = 2;
                literalLengths[60] = 2;
                literalLengths[62] = 2;
                literalLengths[114] = 3;
                literalLengths[256] = 3;
            }

            var distanceLengths = new byte[] { singletonDistanceTree ? (byte)1 : (byte)0 };
            var codeLengthLengths = new byte[19];
            codeLengthLengths[0] = 2;
            codeLengthLengths[1] = 2;
            codeLengthLengths[2] = 2;
            codeLengthLengths[3] = 2;

            var writer = new DeflateBitWriter();
            writer.WriteBits(1, 1);
            writer.WriteBits(2, 2);
            writer.WriteBits(literalLengthCount - 257, 5);
            writer.WriteBits(0, 5);
            writer.WriteBits(CodeLengthOrder.Length - 4, 4);
            foreach (var symbol in CodeLengthOrder)
            {
                writer.WriteBits(codeLengthLengths[symbol], 3);
            }

            var codeLengthCodes = CreateCodes(codeLengthLengths);
            foreach (var length in literalLengths.Concat(distanceLengths))
            {
                writer.WriteCode(codeLengthCodes[length]);
            }

            var literalCodes = CreateCodes(literalLengths);
            foreach (var value in "<r/>"u8)
            {
                writer.WriteCode(literalCodes[value]);
            }

            if (emitLengthWithoutDistance)
            {
                writer.WriteCode(literalCodes[257]);
            }
            else
            {
                writer.WriteCode(literalCodes[256]);
            }

            var deflate = writer.ToArray();
            using var zlib = new MemoryStream();
            zlib.WriteByte(0x78);
            zlib.WriteByte(0x9C);
            zlib.Write(deflate);
            Span<byte> adler = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(adler, ComputeAdler32("<r/>"u8));
            zlib.Write(adler);
            return zlib.ToArray();
        }

        private static HuffmanCode[] CreateCodes(byte[] lengths)
        {
            var maximumLength = lengths.Max();
            var counts = new int[maximumLength + 1];
            foreach (var length in lengths)
            {
                if (length != 0)
                {
                    counts[length]++;
                }
            }

            var nextCodes = new int[maximumLength + 1];
            var code = 0;
            for (var bits = 1; bits <= maximumLength; bits++)
            {
                code = (code + counts[bits - 1]) << 1;
                nextCodes[bits] = code;
            }

            var result = new HuffmanCode[lengths.Length];
            for (var symbol = 0; symbol < lengths.Length; symbol++)
            {
                var length = lengths[symbol];
                if (length != 0)
                {
                    result[symbol] = new HuffmanCode(
                        ReverseBits(nextCodes[length]++, length),
                        length);
                }
            }

            return result;
        }

        private static int ReverseBits(int value, int bitCount)
        {
            var reversed = 0;
            for (var index = 0; index < bitCount; index++)
            {
                reversed = (reversed << 1) | (value & 1);
                value >>= 1;
            }

            return reversed;
        }

        private static uint ComputeAdler32(ReadOnlySpan<byte> data)
        {
            const uint modulus = 65521;
            uint first = 1;
            uint second = 0;
            foreach (var value in data)
            {
                first = (first + value) % modulus;
                second = (second + first) % modulus;
            }

            return (second << 16) | first;
        }

        private readonly record struct HuffmanCode(int Bits, int Length);

        private sealed class DeflateBitWriter
        {
            private readonly List<byte> _bytes = [];
            private int _bitPosition;

            internal void WriteCode(HuffmanCode code) => WriteBits(code.Bits, code.Length);

            internal void WriteBits(int value, int bitCount)
            {
                for (var index = 0; index < bitCount; index++)
                {
                    if ((_bitPosition & 7) == 0)
                    {
                        _bytes.Add(0);
                    }

                    if (((value >> index) & 1) != 0)
                    {
                        _bytes[^1] |= checked((byte)(1 << (_bitPosition & 7)));
                    }

                    _bitPosition++;
                }
            }

            internal byte[] ToArray() => _bytes.ToArray();
        }
    }
}
