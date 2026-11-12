using System.Buffers.Binary;

namespace MIPLabelCustomCheck.Internal;

internal static class JpegLabelReader
{
    private const int SegmentHeaderLength = 2;
    private const int MaxAggregateXmpBytes = PngLabelReader.MaxAggregateXmpBytes;
    private const byte App1Marker = 0xE1;
    private const byte StartOfScanMarker = 0xDA;
    private const byte EndOfImageMarker = 0xD9;

    private static ReadOnlySpan<byte> XmpIdentifier => "http://ns.adobe.com/xap/1.0/\0"u8;

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
        catch (JpegStructureException)
        {
            return LabelReadResult.Invalid();
        }
        catch (InvalidXmpPacketException)
        {
            return LabelReadResult.Invalid();
        }
    }

    private static LabelReadResult ReadCore(Stream stream, bool includeDetails)
    {
        if (stream.Length < 3 || ReadByte(stream) != 0xFF || ReadByte(stream) != 0xD8)
        {
            throw new JpegStructureException("The JPEG SOI marker is missing.");
        }

        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var details = includeDetails ? new List<LabelDetail>() : null;
        var aggregateXmpBytes = 0;
        var identifierBuffer = new byte[XmpIdentifier.Length];

        while (stream.Position < stream.Length)
        {
            var marker = ReadMarker(stream);
            if (marker == EndOfImageMarker || marker == StartOfScanMarker)
            {
                return ToResult(labelIds, details);
            }

            if (IsStandaloneMarker(marker))
            {
                continue;
            }

            var payloadLength = ReadSegmentPayloadLength(stream);
            if (stream.Length - stream.Position < payloadLength)
            {
                throw new JpegStructureException("The JPEG segment extends beyond the stream.");
            }

            if (marker != App1Marker || payloadLength < XmpIdentifier.Length)
            {
                stream.Seek(payloadLength, SeekOrigin.Current);
                continue;
            }

            ReadExactly(stream, identifierBuffer);
            var remainingPayload = payloadLength - XmpIdentifier.Length;
            if (!identifierBuffer.AsSpan().SequenceEqual(XmpIdentifier))
            {
                stream.Seek(remainingPayload, SeekOrigin.Current);
                continue;
            }

            if (remainingPayload <= 0 || aggregateXmpBytes > MaxAggregateXmpBytes - remainingPayload)
            {
                throw new JpegStructureException("The JPEG XMP metadata exceeds the byte limit.");
            }

            var xmpLength = InputGuard.CheckedLength(
                remainingPayload,
                PngLabelReader.MaxDecodedXmpBytes,
                "The JPEG XMP segment");
            var xmp = new byte[xmpLength];
            ReadExactly(stream, xmp);
            aggregateXmpBytes += xmpLength;
            XmpPacketLabelReader.AddLabels(xmp, includeDetails, labelIds, seenLabelIds, details);
        }

        throw new JpegStructureException("The JPEG file ended before SOS or EOI.");
    }

    private static LabelReadResult ToResult(
        List<Guid> labelIds,
        IReadOnlyList<LabelDetail>? details) =>
        labelIds.Count == 0
            ? LabelReadResult.Unlabeled()
            : LabelReadResult.Labeled(
                labelIds,
                ProtectionType.None,
                details,
                labelIds.Select(_ => LabelSource.Xmp).ToArray());

    private static byte ReadMarker(Stream stream)
    {
        if (ReadByte(stream) != 0xFF)
        {
            throw new JpegStructureException("The JPEG marker prefix is missing.");
        }

        byte marker;
        do
        {
            marker = ReadByte(stream);
        }
        while (marker == 0xFF);

        if (marker == 0x00)
        {
            throw new JpegStructureException("The JPEG marker is byte-stuffed outside scan data.");
        }

        return marker;
    }

    private static int ReadSegmentPayloadLength(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[SegmentHeaderLength];
        ReadExactly(stream, buffer);
        var declaredLength = BinaryPrimitives.ReadUInt16BigEndian(buffer);
        if (declaredLength < SegmentHeaderLength)
        {
            throw new JpegStructureException("The JPEG segment length is invalid.");
        }

        return declaredLength - SegmentHeaderLength;
    }

    private static bool IsStandaloneMarker(byte marker) =>
        marker == 0x01 || marker is >= 0xD0 and <= 0xD7;

    private static byte ReadByte(Stream stream)
    {
        var value = stream.ReadByte();
        if (value < 0)
        {
            throw new JpegStructureException("The JPEG stream is truncated.");
        }

        return checked((byte)value);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = stream.Read(buffer[bytesRead..]);
            if (read == 0)
            {
                throw new JpegStructureException("The JPEG stream is truncated.");
            }

            bytesRead += read;
        }
    }

    private sealed class JpegStructureException : Exception
    {
        internal JpegStructureException(string message)
            : base(message)
        {
        }
    }
}
