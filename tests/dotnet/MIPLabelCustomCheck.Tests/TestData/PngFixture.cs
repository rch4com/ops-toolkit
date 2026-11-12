using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests.TestData;

internal sealed record PngITxtOptions(
    string Text = "<root />",
    string Keyword = PngFixture.XmpKeyword,
    byte CompressionFlag = 0,
    byte CompressionMethod = 0,
    string LanguageTag = "",
    string TranslatedKeyword = "",
    byte[]? TextBytes = null,
    byte[]? LanguageTagBytes = null,
    byte[]? TranslatedKeywordBytes = null,
    bool CompressText = true,
    bool CorruptCrc = false,
    uint? DeclaredLength = null);

internal sealed record PngFixtureData(
    byte[] Bytes,
    int? IdatDataOffset,
    int IdatDataLength);

internal sealed class PngFixture
{
    internal const string XmpKeyword = "XML:com.adobe.xmp";

    private static readonly byte[] Signature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly List<Chunk> _chunks = [];
    private bool _validSignature = true;
    private bool _includeIhdr = true;
    private int _ihdrLength = 13;
    private bool _includeIend = true;
    private int _iendLength;

    internal PngFixture WithBadSignature()
    {
        _validSignature = false;
        return this;
    }

    internal PngFixture WithoutIhdr()
    {
        _includeIhdr = false;
        return this;
    }

    internal PngFixture WithIhdrLength(int length)
    {
        _ihdrLength = length;
        return this;
    }

    internal PngFixture AddITxt(PngITxtOptions options)
    {
        var text = options.TextBytes ?? Encoding.UTF8.GetBytes(options.Text);
        if (options.CompressionFlag == 1 && options.CompressText)
        {
            text = Compress(text);
        }

        using var data = new MemoryStream();
        data.Write(Encoding.Latin1.GetBytes(options.Keyword));
        data.WriteByte(0);
        data.WriteByte(options.CompressionFlag);
        data.WriteByte(options.CompressionMethod);
        data.Write(options.LanguageTagBytes ?? Encoding.ASCII.GetBytes(options.LanguageTag));
        data.WriteByte(0);
        data.Write(
            options.TranslatedKeywordBytes ?? Encoding.UTF8.GetBytes(options.TranslatedKeyword));
        data.WriteByte(0);
        data.Write(text);

        return AddChunk(
            "iTXt",
            data.ToArray(),
            options.CorruptCrc,
            options.DeclaredLength);
    }

    internal PngFixture AddRawITxt(
        byte[] data,
        bool corruptCrc = false,
        uint? declaredLength = null) =>
        AddChunk("iTXt", data, corruptCrc, declaredLength);

    /// <summary>
    /// Adds a real (not merely declared) XMP <c>iTXt</c> chunk one byte past
    /// <see cref="PngLabelReader.MaxXmpChunkBytes"/>, the production reader's per-chunk cap, so it
    /// is rejected before the chunk's text is decoded.
    /// </summary>
    internal PngFixture WithOversizedDeclaredChunk()
    {
        const int nonTextFieldBytes = 5; // NUL after keyword + flag + method + NUL + NUL (empty language/translated keyword)
        var textLength = PngLabelReader.MaxXmpChunkBytes + 1 - XmpKeyword.Length - nonTextFieldBytes;
        return AddITxt(new PngITxtOptions(TextBytes: new byte[textLength], CompressionFlag: 0));
    }

    internal PngFixture WithIdat(byte[] data) => AddChunk("IDAT", data);

    internal PngFixture AddChunk(
        string type,
        byte[] data,
        bool corruptCrc = false,
        uint? declaredLength = null)
    {
        _chunks.Add(new Chunk(type, data, corruptCrc, declaredLength));
        return this;
    }

    internal PngFixture WithoutIend()
    {
        _includeIend = false;
        return this;
    }

    internal PngFixture WithIendLength(int length)
    {
        _iendLength = length;
        return this;
    }

    internal MemoryStream Create() => new(CreateData().Bytes);

    internal PngFixtureData CreateData()
    {
        using var stream = new MemoryStream();
        var signature = (byte[])Signature.Clone();
        if (!_validSignature)
        {
            signature[0] ^= 0xFF;
        }

        stream.Write(signature);

        if (_includeIhdr)
        {
            WriteChunk(stream, new Chunk("IHDR", new byte[_ihdrLength], false, null));
        }

        int? idatDataOffset = null;
        var idatDataLength = 0;
        foreach (var chunk in _chunks)
        {
            if (chunk.Type == "IDAT" && idatDataOffset is null)
            {
                idatDataOffset = checked((int)stream.Position + 8);
                idatDataLength = chunk.Data.Length;
            }

            WriteChunk(stream, chunk);
        }

        if (_includeIend)
        {
            WriteChunk(stream, new Chunk("IEND", new byte[_iendLength], false, null));
        }

        return new PngFixtureData(stream.ToArray(), idatDataOffset, idatDataLength);
    }

    private static void WriteChunk(Stream stream, Chunk chunk)
    {
        var type = Encoding.ASCII.GetBytes(chunk.Type);
        if (type.Length != 4)
        {
            throw new InvalidOperationException("PNG chunk types must contain four ASCII bytes.");
        }

        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(
            integer,
            chunk.DeclaredLength ?? checked((uint)chunk.Data.Length));
        stream.Write(integer);
        stream.Write(type);
        stream.Write(chunk.Data);

        var crc = ComputeCrc(type, chunk.Data);
        if (chunk.CorruptCrc)
        {
            crc ^= 1;
        }

        BinaryPrimitives.WriteUInt32BigEndian(integer, crc);
        stream.Write(integer);
    }

    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(bytes);
        }

        return output.ToArray();
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        Update(ref crc, type);
        Update(ref crc, data);
        return ~crc;
    }

    private static void Update(ref uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
            }
        }
    }

    private sealed record Chunk(
        string Type,
        byte[] Data,
        bool CorruptCrc,
        uint? DeclaredLength);
}
