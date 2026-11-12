using System.Buffers.Binary;

namespace MIPLabelCustomCheck.Internal;

internal static class TiffLabelReader
{
    private const ushort TiffMagic = 42;
    private const ushort XmpTag = 700;
    private const int HeaderLength = 8;
    private const int IfdEntryLength = 12;
    private const int MaxAggregateXmpBytes = PngLabelReader.MaxAggregateXmpBytes;

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
        catch (TiffStructureException)
        {
            return LabelReadResult.Invalid();
        }
        catch (InvalidXmpPacketException)
        {
            return LabelReadResult.Invalid();
        }
        catch (InvalidDataException)
        {
            return LabelReadResult.Invalid();
        }
    }

    private static LabelReadResult ReadCore(Stream stream, bool includeDetails)
    {
        if (stream.Length < HeaderLength)
        {
            throw new TiffStructureException("The TIFF header is truncated.");
        }

        Span<byte> header = stackalloc byte[HeaderLength];
        ReadExactly(stream, header);
        var littleEndian = header[0] == 0x49 && header[1] == 0x49;
        if (!littleEndian && !(header[0] == 0x4D && header[1] == 0x4D))
        {
            throw new TiffStructureException("The TIFF byte order marker is invalid.");
        }

        if (ReadUInt16(header[2..4], littleEndian) != TiffMagic)
        {
            throw new TiffStructureException("The TIFF magic value is invalid.");
        }

        var nextIfdOffset = ReadUInt32(header[4..8], littleEndian);
        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var details = includeDetails ? new List<LabelDetail>() : null;
        var aggregateXmpBytes = 0;
        var ifdCount = 0;
        var countBuffer = new byte[sizeof(ushort)];
        var entryBuffer = new byte[IfdEntryLength];
        var nextBuffer = new byte[sizeof(uint)];

        while (nextIfdOffset != 0)
        {
            if (++ifdCount > 1024)
            {
                throw new TiffStructureException("The TIFF IFD chain is too long.");
            }

            EnsureRange(nextIfdOffset, sizeof(ushort), stream.Length);
            stream.Position = nextIfdOffset;
            ReadExactly(stream, countBuffer);
            var entryCount = ReadUInt16(countBuffer, littleEndian);
            var entriesBytes = checked((long)entryCount * IfdEntryLength);
            EnsureRange(stream.Position, entriesBytes + sizeof(uint), stream.Length);

            for (var index = 0; index < entryCount; index++)
            {
                ReadExactly(stream, entryBuffer);
                ReadOnlySpan<byte> entry = entryBuffer;
                if (ReadUInt16(entry[..2], littleEndian) != XmpTag)
                {
                    continue;
                }

                var valueCount = ReadUInt32(entry[4..8], littleEndian);
                if (valueCount == 0 || valueCount > PngLabelReader.MaxDecodedXmpBytes)
                {
                    throw new TiffStructureException("The TIFF XMP tag exceeds the byte limit.");
                }

                var xmpLength = InputGuard.CheckedLength(
                    valueCount,
                    PngLabelReader.MaxDecodedXmpBytes,
                    "The TIFF XMP tag");
                if (aggregateXmpBytes > MaxAggregateXmpBytes - xmpLength)
                {
                    throw new TiffStructureException("The TIFF XMP metadata exceeds the byte limit.");
                }

                var valueOffset = xmpLength <= sizeof(uint)
                    ? -1L
                    : ReadUInt32(entry[8..12], littleEndian);
                var xmp = new byte[xmpLength];
                if (valueOffset < 0)
                {
                    entry[8..(8 + xmpLength)].CopyTo(xmp);
                }
                else
                {
                    EnsureRange(valueOffset, xmpLength, stream.Length);
                    var returnPosition = stream.Position;
                    stream.Position = valueOffset;
                    ReadExactly(stream, xmp);
                    stream.Position = returnPosition;
                }

                aggregateXmpBytes += xmpLength;
                XmpPacketLabelReader.AddLabels(xmp, includeDetails, labelIds, seenLabelIds, details);
            }

            ReadExactly(stream, nextBuffer);
            nextIfdOffset = ReadUInt32(nextBuffer, littleEndian);
        }

        return labelIds.Count == 0
            ? LabelReadResult.Unlabeled()
            : LabelReadResult.Labeled(
                labelIds,
                ProtectionType.None,
                details,
                labelIds.Select(_ => LabelSource.Xmp).ToArray());
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);

    private static void EnsureRange(long offset, long length, long streamLength)
    {
        if (offset < 0 || length < 0 || offset > streamLength || streamLength - offset < length)
        {
            throw new TiffStructureException("The TIFF offset extends beyond the stream.");
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = stream.Read(buffer[bytesRead..]);
            if (read == 0)
            {
                throw new TiffStructureException("The TIFF stream is truncated.");
            }

            bytesRead += read;
        }
    }

    private sealed class TiffStructureException : Exception
    {
        internal TiffStructureException(string message)
            : base(message)
        {
        }
    }
}
