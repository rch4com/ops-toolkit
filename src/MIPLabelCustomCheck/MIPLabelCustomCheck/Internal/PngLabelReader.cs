using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace MIPLabelCustomCheck.Internal;

internal static class PngLabelReader
{
    internal const int MaxDecodedXmpBytes = 4 * 1024 * 1024;
    internal const int MaxAggregateXmpBytes = 4 * 1024 * 1024;
    internal const int MaxXmpChunkBytes = MaxDecodedXmpBytes + 1024;
    internal const int MaxXmpChunkCount = 64;

    private const int PngSignatureLength = 8;
    private const int IhdrLength = 13;
    private const int MaxKeywordBytes = 79;
    private const uint IhdrType = 0x49484452;
    private const uint ItxtType = 0x69545874;
    private const uint IendType = 0x49454E44;

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> XmpKeyword => "XML:com.adobe.xmp"u8;

    internal static LabelReadResult Read(Stream stream, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new NotSupportedException("The stream must be seekable.");
        }

        try
        {
            return ReadCore(stream, includeDetails);
        }
        catch (PngStructureException)
        {
            return LabelReadResult.Invalid();
        }
    }

    private static LabelReadResult ReadCore(Stream stream, bool includeDetails)
    {
        Span<byte> signature = stackalloc byte[PngSignatureLength];
        ReadExactly(stream, signature);
        if (!signature.SequenceEqual(PngSignature))
        {
            throw new PngStructureException("The PNG signature is invalid.");
        }

        var firstChunk = ReadChunkHeader(stream);
        if (firstChunk.Type != IhdrType || firstChunk.Length != IhdrLength)
        {
            throw new PngStructureException("The first PNG chunk must be a 13-byte IHDR.");
        }

        SkipPayloadAndCrc(stream, firstChunk.Length);

        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var details = includeDetails ? new List<LabelDetail>() : null;
        var xmpChunkCount = 0;
        var aggregateXmpBytes = 0;

        while (stream.Position < stream.Length)
        {
            var chunk = ReadChunkHeader(stream);
            if (chunk.Type == IendType)
            {
                if (chunk.Length != 0)
                {
                    throw new PngStructureException("The PNG IEND chunk must be empty.");
                }

                SkipPayloadAndCrc(stream, chunk.Length);
                return labelIds.Count == 0
                    ? LabelReadResult.Unlabeled()
                    : LabelReadResult.Labeled(
                        labelIds,
                        ProtectionType.None,
                        details,
                        labelIds.Select(_ => LabelSource.Xmp).ToArray());
            }

            if (chunk.Type != ItxtType)
            {
                // MIP clients record XMP in the iTXt chunk with keyword "XML:com.adobe.xmp";
                // zTXt/tEXt chunks are skipped by design and never treated as label metadata.
                SkipPayloadAndCrc(stream, chunk.Length);
                continue;
            }

            var xmpPacket = ReadXmpITxt(stream, chunk.Length, ref xmpChunkCount);
            if (xmpPacket is null)
            {
                continue;
            }

            if (xmpPacket.DecodedLength == 0 ||
                xmpPacket.DecodedLength > MaxDecodedXmpBytes ||
                aggregateXmpBytes > MaxAggregateXmpBytes - xmpPacket.DecodedLength)
            {
                throw new PngStructureException("The PNG XMP metadata exceeds the byte limit.");
            }

            aggregateXmpBytes += xmpPacket.DecodedLength;

            using var xmpStream = new MemoryStream(xmpPacket.Data, writable: false);
            LabelPropertyParseResult parsed;
            try
            {
                parsed = XmpLabelReader.Read(xmpStream, xmpPacket.Data.Length, includeDetails);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or XmlException or DecoderFallbackException)
            {
                throw new PngStructureException("The PNG XMP metadata is invalid.", exception);
            }

            if (parsed.InvalidMetadata)
            {
                return LabelReadResult.Invalid();
            }

            foreach (var labelId in parsed.LabelIds)
            {
                if (!seenLabelIds.Add(labelId))
                {
                    continue;
                }

                labelIds.Add(labelId);

                // 여러 XMP 청크에 같은 레이블이 있으면 먼저 본 청크의 부가 정보를 쓴다 —
                // 레이블 ID를 중복 제거하는 규칙과 같다.
                if (details is not null &&
                    parsed.Details.FirstOrDefault(d => d.Id == labelId) is { } detail)
                {
                    details.Add(detail);
                }
            }
        }

        throw new PngStructureException("The PNG file has no IEND chunk.");
    }

    private static XmpPacket? ReadXmpITxt(
        Stream stream,
        uint chunkLength,
        ref int xmpChunkCount)
    {
        var dataStart = stream.Position;
        var keywordMatches = true;
        var keywordLength = 0;
        Span<byte> valueBuffer = stackalloc byte[1];

        while (keywordLength <= MaxKeywordBytes && keywordLength < chunkLength)
        {
            ReadExactly(stream, valueBuffer);
            var value = valueBuffer[0];

            if (value == 0)
            {
                if (keywordLength == XmpKeyword.Length && keywordMatches)
                {
                    xmpChunkCount++;
                    if (xmpChunkCount > MaxXmpChunkCount)
                    {
                        throw new PngStructureException("The PNG contains too many XMP chunks.");
                    }

                    return ReadAndValidateXmpData(stream, dataStart, chunkLength);
                }

                SkipRemainingDataAndCrc(stream, dataStart, chunkLength);
                return null;
            }

            if (keywordLength >= XmpKeyword.Length || value != XmpKeyword[keywordLength])
            {
                keywordMatches = false;
                SkipRemainingDataAndCrc(stream, dataStart, chunkLength);
                return null;
            }

            keywordLength++;
        }

        if (keywordMatches &&
            keywordLength == XmpKeyword.Length &&
            keywordLength == chunkLength)
        {
            throw new PngStructureException("The PNG XMP keyword is not terminated.");
        }

        SkipRemainingDataAndCrc(stream, dataStart, chunkLength);
        return null;
    }

    private static XmpPacket ReadAndValidateXmpData(
        Stream stream,
        long dataStart,
        uint chunkLength)
    {
        int length;
        try
        {
            length = InputGuard.CheckedLength(chunkLength, MaxXmpChunkBytes, "The PNG XMP chunk");
        }
        catch (InvalidDataException exception)
        {
            throw new PngStructureException("The PNG XMP chunk exceeds the byte limit.", exception);
        }

        stream.Position = dataStart;
        var data = new byte[length];
        ReadExactly(stream, data);
        var storedCrc = ReadUInt32(stream);
        var computedCrc = Crc32.Compute("iTXt"u8, data);
        if (storedCrc != computedCrc)
        {
            throw new PngStructureException("The PNG XMP chunk CRC is invalid.");
        }

        try
        {
            return ParseITxtData(data);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or DecoderFallbackException)
        {
            throw new PngStructureException("The PNG XMP iTXt data is invalid.", exception);
        }
    }

    private static XmpPacket ParseITxtData(byte[] data)
    {
        var index = XmpKeyword.Length + 1;
        if (data.Length - index < 2)
        {
            throw new PngStructureException("The PNG XMP iTXt fields are incomplete.");
        }

        var compressionFlag = data[index++];
        var compressionMethod = data[index++];
        if (compressionFlag is not (0 or 1) || compressionMethod != 0)
        {
            throw new PngStructureException("The PNG XMP compression fields are invalid.");
        }

        var languageEnd = FindNull(data, index);
        foreach (var value in data.AsSpan(index, languageEnd - index))
        {
            if (value > 0x7F)
            {
                throw new DecoderFallbackException("The PNG XMP language tag is not ASCII.");
            }
        }

        index = languageEnd + 1;
        var translatedKeywordEnd = FindNull(data, index);
        _ = StrictUtf8.GetCharCount(data.AsSpan(index, translatedKeywordEnd - index));
        index = translatedKeywordEnd + 1;

        var decoded = compressionFlag == 0
            ? data.AsSpan(index).ToArray()
            : Decompress(data, index);

        if (decoded.Length > MaxDecodedXmpBytes)
        {
            throw new PngStructureException("The decoded PNG XMP exceeds the byte limit.");
        }

        _ = StrictUtf8.GetCharCount(decoded);
        return new XmpPacket(GetPacketXml(decoded), decoded.Length);
    }

    private static byte[] GetPacketXml(byte[] decoded)
    {
        ReadOnlySpan<byte> content = decoded;
        if (content.StartsWith("\uFEFF"u8))
        {
            content = content[3..];
        }

        if (!content.StartsWith("<?xpacket begin="u8))
        {
            return decoded;
        }

        ReadOnlySpan<byte> duplicatedTrailer = "<?xpacket end=\"r\"?>=\"r\"?>"u8;
        if (!content.EndsWith(duplicatedTrailer))
        {
            return decoded;
        }

        return decoded.AsSpan(0, decoded.Length - "=\"r\"?>"u8.Length).ToArray();
    }

    private static byte[] Decompress(byte[] data, int offset)
    {
        try
        {
            return DecompressCore(data, offset);
        }
        catch (InvalidDataException exception)
        {
            throw new PngStructureException("The PNG XMP zlib payload is invalid.", exception);
        }
    }

    private static byte[] DecompressCore(byte[] data, int offset)
    {
        using var compressed = new MemoryStream(
            data,
            offset,
            data.Length - offset,
            writable: false,
            publiclyVisible: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        using var decoded = new MemoryStream();
        var buffer = new byte[8192];

        while (true)
        {
            var read = zlib.Read(buffer);
            if (read == 0)
            {
                break;
            }

            if (decoded.Length > MaxDecodedXmpBytes - read)
            {
                throw new PngStructureException("The decoded PNG XMP exceeds the byte limit.");
            }

            decoded.Write(buffer, 0, read);
        }

        var decodedBytes = decoded.ToArray();
        DeflateFramingValidator.Validate(
            data.AsSpan(offset),
            decodedBytes,
            MaxDecodedXmpBytes);
        return decodedBytes;
    }

    private static int FindNull(byte[] data, int start)
    {
        var relativeIndex = data.AsSpan(start).IndexOf((byte)0);
        if (relativeIndex < 0)
        {
            throw new PngStructureException("The PNG XMP iTXt fields are incomplete.");
        }

        return start + relativeIndex;
    }

    private static ChunkHeader ReadChunkHeader(Stream stream)
    {
        if (stream.Length - stream.Position < 8)
        {
            throw new PngStructureException("The PNG chunk header is truncated.");
        }

        Span<byte> header = stackalloc byte[8];
        ReadExactly(stream, header);

        var typeBytes = header[4..8];
        foreach (var value in typeBytes)
        {
            if (value is not (>= (byte)'A' and <= (byte)'Z') and
                not (>= (byte)'a' and <= (byte)'z'))
            {
                throw new PngStructureException("The PNG chunk type is invalid.");
            }
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
        if (length > 0x7FFFFFFF)
        {
            throw new PngStructureException("The PNG chunk length has its high bit set.");
        }

        var remaining = stream.Length - stream.Position;
        if (remaining < 0 || remaining < (long)length + sizeof(uint))
        {
            throw new PngStructureException("The PNG chunk extends beyond the stream.");
        }

        return new ChunkHeader(
            length,
            BinaryPrimitives.ReadUInt32BigEndian(typeBytes));
    }

    private static void SkipPayloadAndCrc(Stream stream, uint length)
    {
        stream.Seek(length, SeekOrigin.Current);
        _ = ReadUInt32(stream);
    }

    private static void SkipRemainingDataAndCrc(
        Stream stream,
        long dataStart,
        uint chunkLength)
    {
        stream.Seek(dataStart + chunkLength - stream.Position, SeekOrigin.Current);
        _ = ReadUInt32(stream);
    }

    private static uint ReadUInt32(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[4];
        ReadExactly(stream, buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = stream.Read(buffer[bytesRead..]);
            if (read == 0)
            {
                throw new PngStructureException("The PNG stream is truncated.");
            }

            bytesRead += read;
        }
    }

    private readonly record struct ChunkHeader(uint Length, uint Type);

    private sealed record XmpPacket(byte[] Data, int DecodedLength);

    private sealed class PngStructureException : Exception
    {
        internal PngStructureException(string message)
            : base(message)
        {
        }

        internal PngStructureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
