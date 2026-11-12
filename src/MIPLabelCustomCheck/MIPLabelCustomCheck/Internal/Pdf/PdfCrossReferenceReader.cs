using System.IO.Compression;

namespace MIPLabelCustomCheck.Internal.Pdf;

internal enum PdfXrefEntryType
{
    Free = 0,
    InUse = 1,
    Compressed = 2
}

internal readonly record struct PdfXrefEntry(PdfXrefEntryType Type, long Field2, int Field3);

/// <summary>The merged result of following a PDF's <c>startxref</c>/<c>/Prev</c> revision chain.</summary>
internal sealed record PdfCrossReferenceTable(
    PdfDictionary Trailer,
    IReadOnlyDictionary<int, PdfXrefEntry> Entries);

/// <summary>
/// Locates the newest revision's cross-reference table (classic or stream) and follows
/// <c>/Prev</c> back through older revisions, merging entries so that the newest revision's
/// object offsets win. Only reads bounded slices of the underlying stream: a growing tail window
/// (64 KiB up to 4 MiB) to find <c>startxref</c>, and a capped per-object slice for each revision's
/// table/trailer or cross-reference stream.
/// </summary>
internal static class PdfCrossReferenceReader
{
    internal const int MinSearchWindowBytes = 64 * 1024;
    internal const int MaxSearchWindowBytes = 4 * 1024 * 1024;
    internal const int MaxRevisions = 1024;
    internal const int MaxXrefEntries = 1_000_000;

    /// <summary>
    /// Upper bound on the number of bytes read in one shot to parse a single revision's
    /// xref section (classic table + trailer, or an indirect xref-stream object). Comfortably
    /// exceeds <see cref="PdfLexer.MaxStreamBytes"/> so that a stream's raw bytes are always
    /// fully contained within this same slice.
    /// </summary>
    internal const int MaxIndirectObjectSliceBytes = 20 * 1024 * 1024;

    private static ReadOnlySpan<byte> StartXrefKeyword => "startxref"u8;

    internal static PdfCrossReferenceTable Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            return ReadCore(stream);
        }
        catch (OverflowException exception)
        {
            throw Invalid("The PDF cross-reference structure contains an overflowing size or offset.", exception);
        }
    }

    /// <summary>
    /// Decodes a stream's raw bytes according to its <c>/Filter</c>: absent means the bytes are
    /// already raw; <c>/FlateDecode</c> (bare or as a single-element array) is inflated and
    /// validated; anything else is an unsupported (not corrupt) filter chain.
    /// </summary>
    internal static byte[] DecodeStreamData(PdfStream pdfStream)
    {
        if (!pdfStream.Dictionary.Items.TryGetValue("Filter", out var filterValue))
        {
            return pdfStream.Data;
        }

        var isFlateDecode = filterValue switch
        {
            PdfName { Value: "FlateDecode" } => true,
            PdfArray { Items: [PdfName { Value: "FlateDecode" }] } => true,
            _ => false
        };

        if (!isFlateDecode)
        {
            throw new PdfUnsupportedFeatureException("The PDF stream uses an unsupported /Filter.");
        }

        return InflateFlateDecode(pdfStream.Data);
    }

    private static PdfCrossReferenceTable ReadCore(Stream stream)
    {
        var startOffset = FindStartXrefOffset(stream);
        var entries = new Dictionary<int, PdfXrefEntry>();
        var visitedOffsets = new HashSet<long>();
        PdfDictionary? latestTrailer = null;
        var currentOffset = startOffset;
        var revisionCount = 0;

        while (true)
        {
            if (revisionCount >= MaxRevisions)
            {
                throw Invalid("The PDF /Prev chain exceeds the maximum supported revision count.");
            }

            if (!visitedOffsets.Add(currentOffset))
            {
                throw Invalid("The PDF /Prev chain contains a cycle.");
            }

            revisionCount++;

            var section = ReadSection(stream, currentOffset);
            foreach (var pair in section.Entries)
            {
                entries.TryAdd(pair.Key, pair.Value);
            }

            latestTrailer ??= section.Trailer;

            if (!TryGetPrevOffset(section.Trailer, out var prevOffset))
            {
                break;
            }

            currentOffset = prevOffset;
        }

        return new PdfCrossReferenceTable(latestTrailer!, entries);
    }

    private static long FindStartXrefOffset(Stream stream)
    {
        var fileLength = stream.Length;
        if (fileLength <= 0)
        {
            throw Invalid("The PDF file is empty.");
        }

        var windowSize = (int)Math.Min(MinSearchWindowBytes, fileLength);
        while (true)
        {
            var start = fileLength - windowSize;
            var buffer = new byte[windowSize];
            ReadExactlyAt(stream, start, buffer);

            var index = buffer.AsSpan().LastIndexOf(StartXrefKeyword);
            if (index >= 0)
            {
                var afterKeyword = index + StartXrefKeyword.Length;
                var offsetStart = PdfLexer.SkipWhitespaceAndComments(buffer, afterKeyword);
                var lexer = new PdfLexer(buffer.AsMemory(offsetStart));
                if (lexer.ReadValue() is not PdfNumber { Value: >= 0 } offsetNumber)
                {
                    throw Invalid("The PDF startxref value is not a valid non-negative integer.");
                }

                return offsetNumber.Value;
            }

            var maxPossibleWindow = (int)Math.Min(MaxSearchWindowBytes, fileLength);
            if (windowSize >= maxPossibleWindow)
            {
                throw Invalid("The PDF file has no 'startxref' keyword within the search window.");
            }

            windowSize = (int)Math.Min((long)windowSize * 2, maxPossibleWindow);
        }
    }

    private static XrefSection ReadSection(Stream stream, long offset)
    {
        if (offset < 0 || offset >= stream.Length)
        {
            throw Invalid("A PDF xref offset is outside the file.");
        }

        var sliceLength = (int)Math.Min(MaxIndirectObjectSliceBytes, stream.Length - offset);
        var buffer = new byte[sliceLength];
        ReadExactlyAt(stream, offset, buffer);

        var probePosition = PdfLexer.SkipWhitespaceAndComments(buffer, 0);
        return PdfLexer.TryMatchKeyword(buffer, probePosition, "xref", out _)
            ? ReadClassicSection(buffer)
            : ReadStreamSection(buffer);
    }

    private static XrefSection ReadClassicSection(ReadOnlyMemory<byte> buffer)
    {
        var pos = PdfLexer.SkipWhitespaceAndComments(buffer.Span, 0);
        if (!PdfLexer.TryMatchKeyword(buffer.Span, pos, "xref", out pos))
        {
            throw Invalid("A classic PDF xref section is missing the 'xref' keyword.");
        }

        var entries = new Dictionary<int, PdfXrefEntry>();

        while (true)
        {
            pos = PdfLexer.SkipWhitespaceAndComments(buffer.Span, pos);
            if (PdfLexer.TryMatchKeyword(buffer.Span, pos, "trailer", out var afterTrailer))
            {
                pos = afterTrailer;
                break;
            }

            var headerLexer = new PdfLexer(buffer[pos..]);
            var firstValue = headerLexer.ReadValue();
            var countValue = headerLexer.ReadValue();
            pos += headerLexer.Position;

            if (firstValue is not PdfNumber { Value: >= 0 } firstNumber ||
                countValue is not PdfNumber { Value: >= 0 and <= MaxXrefEntries } countNumber)
            {
                throw Invalid("A classic PDF xref subsection header is invalid.");
            }

            pos = PdfLexer.SkipWhitespaceAndComments(buffer.Span, pos);

            for (var index = 0; index < countNumber.Value; index++)
            {
                if (pos + 20 > buffer.Length)
                {
                    throw Invalid("A classic PDF xref entry is truncated.");
                }

                var entry = ParseClassicEntry(buffer.Span.Slice(pos, 20));
                var objectNumber = checked((int)(firstNumber.Value + index));
                entries.TryAdd(objectNumber, entry);
                pos += 20;
            }
        }

        pos = PdfLexer.SkipWhitespaceAndComments(buffer.Span, pos);
        var trailerLexer = new PdfLexer(buffer[pos..]);
        if (trailerLexer.ReadValue() is not PdfDictionary trailerDictionary)
        {
            throw Invalid("The PDF trailer is not a dictionary.");
        }

        return new XrefSection(trailerDictionary, entries);
    }

    private static PdfXrefEntry ParseClassicEntry(ReadOnlySpan<byte> record)
    {
        if (!TryParseFixedDigits(record[..10], out var offset) ||
            record[10] != (byte)' ' ||
            !TryParseFixedDigits(record.Slice(11, 5), out var generation) ||
            record[16] != (byte)' ' ||
            record[17] is not ((byte)'n' or (byte)'f'))
        {
            throw Invalid("A classic PDF xref entry is malformed.");
        }

        var eol = record[18..20];
        var validEol = eol.SequenceEqual("\r\n"u8) || eol.SequenceEqual(" \r"u8) || eol.SequenceEqual(" \n"u8);
        if (!validEol)
        {
            throw Invalid("A classic PDF xref entry has an invalid end-of-line marker.");
        }

        if (generation > int.MaxValue)
        {
            throw Invalid("A classic PDF xref entry generation number is out of range.");
        }

        var type = record[17] == (byte)'n' ? PdfXrefEntryType.InUse : PdfXrefEntryType.Free;
        return new PdfXrefEntry(type, offset, (int)generation);
    }

    private static bool TryParseFixedDigits(ReadOnlySpan<byte> digits, out long value)
    {
        value = 0;
        foreach (var digit in digits)
        {
            if (digit is < (byte)'0' or > (byte)'9')
            {
                value = 0;
                return false;
            }

            value = (value * 10) + (digit - (byte)'0');
        }

        return true;
    }

    private static XrefSection ReadStreamSection(ReadOnlyMemory<byte> buffer)
    {
        var lexer = new PdfLexer(buffer);
        var indirectObject = lexer.ReadIndirectObject();
        if (indirectObject.Value is not PdfStream xrefStream)
        {
            throw Invalid("A PDF xref entry offset does not reference a stream object.");
        }

        var dictionary = xrefStream.Dictionary;
        if (!dictionary.Items.TryGetValue("W", out var widthsValue) ||
            widthsValue is not PdfArray { Items.Count: >= 3 } widthsArray)
        {
            throw Invalid("A PDF cross-reference stream is missing /W.");
        }

        var widths = new int[3];
        for (var index = 0; index < 3; index++)
        {
            if (widthsArray.Items[index] is not PdfNumber { Value: >= 0 and <= 8 } widthNumber)
            {
                throw Invalid("A PDF cross-reference stream /W entry is invalid.");
            }

            widths[index] = (int)widthNumber.Value;
        }

        if (!dictionary.Items.TryGetValue("Size", out var sizeValue) ||
            sizeValue is not PdfNumber { Value: >= 0 and <= MaxXrefEntries })
        {
            throw Invalid("A PDF cross-reference stream /Size is invalid.");
        }

        var subsections = new List<(long Start, long Count)>();
        if (dictionary.Items.TryGetValue("Index", out var indexValue))
        {
            if (indexValue is not PdfArray indexArray || indexArray.Items.Count % 2 != 0)
            {
                throw Invalid("A PDF cross-reference stream /Index is invalid.");
            }

            for (var index = 0; index < indexArray.Items.Count; index += 2)
            {
                if (indexArray.Items[index] is not PdfNumber { Value: >= 0 } start ||
                    indexArray.Items[index + 1] is not PdfNumber { Value: >= 0 and <= MaxXrefEntries } count)
                {
                    throw Invalid("A PDF cross-reference stream /Index entry is invalid.");
                }

                subsections.Add((start.Value, count.Value));
            }
        }
        else
        {
            subsections.Add((0, ((PdfNumber)sizeValue).Value));
        }

        var decoded = DecodeStreamData(xrefStream);
        var rowWidth = widths[0] + widths[1] + widths[2];
        if (rowWidth == 0)
        {
            throw Invalid("A PDF cross-reference stream /W declares a zero total row width.");
        }

        var entries = new Dictionary<int, PdfXrefEntry>();
        var cursor = 0;
        foreach (var (start, count) in subsections)
        {
            for (var index = 0; index < count; index++)
            {
                if (checked(cursor + rowWidth) > decoded.Length)
                {
                    throw Invalid("A PDF cross-reference stream is truncated.");
                }

                var typeField = widths[0] == 0 ? 1 : ReadBigEndian(decoded, cursor, widths[0]);
                cursor += widths[0];
                var field2 = ReadBigEndian(decoded, cursor, widths[1]);
                cursor += widths[1];
                var field3 = widths[2] == 0 ? 0 : ReadBigEndian(decoded, cursor, widths[2]);
                cursor += widths[2];

                if (typeField is < 0 or > 2)
                {
                    throw Invalid("A PDF cross-reference stream entry has an invalid type.");
                }

                var objectNumber = checked((int)(start + index));
                entries.TryAdd(
                    objectNumber,
                    new PdfXrefEntry((PdfXrefEntryType)typeField, field2, checked((int)field3)));
            }
        }

        return new XrefSection(dictionary, entries);
    }

    private static long ReadBigEndian(byte[] data, int offset, int width)
    {
        long value = 0;
        for (var index = 0; index < width; index++)
        {
            value = (value << 8) | data[offset + index];
        }

        return value;
    }

    private static bool TryGetPrevOffset(PdfDictionary trailer, out long prevOffset)
    {
        if (trailer.Items.TryGetValue("Prev", out var value))
        {
            if (value is PdfNumber { Value: >= 0 } number)
            {
                prevOffset = number.Value;
                return true;
            }

            throw Invalid("A PDF trailer /Prev value is invalid.");
        }

        prevOffset = 0;
        return false;
    }

    private static byte[] InflateFlateDecode(byte[] data)
    {
        try
        {
            return InflateFlateDecodeCore(data);
        }
        catch (InvalidDataException exception)
        {
            throw Invalid("The PDF FlateDecode stream is invalid.", exception);
        }
    }

    private static byte[] InflateFlateDecodeCore(byte[] data)
    {
        using var compressed = new MemoryStream(data, writable: false);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: true);
        using var decoded = new MemoryStream();
        var readBuffer = new byte[8192];

        while (true)
        {
            var read = zlib.Read(readBuffer);
            if (read == 0)
            {
                break;
            }

            if (decoded.Length > PdfLexer.MaxStreamBytes - read)
            {
                throw new InvalidDataException("The decoded PDF FlateDecode stream exceeds the byte limit.");
            }

            decoded.Write(readBuffer, 0, read);
        }

        var decodedBytes = decoded.ToArray();
        DeflateFramingValidator.Validate(data, decodedBytes, PdfLexer.MaxStreamBytes);
        return decodedBytes;
    }

    /// <summary>
    /// Reads <paramref name="buffer"/>.Length bytes starting at <paramref name="offset"/>, looping
    /// over short reads. Shared with <see cref="PdfObjectResolver"/> so both callers use the same
    /// truncation handling.
    /// </summary>
    internal static void ReadExactlyAt(Stream stream, long offset, byte[] buffer)
    {
        stream.Position = offset;
        var read = 0;
        while (read < buffer.Length)
        {
            var count = stream.Read(buffer, read, buffer.Length - read);
            if (count == 0)
            {
                throw Invalid("The PDF stream is truncated.");
            }

            read += count;
        }
    }

    private static InvalidDataException Invalid(string message) => new(message);

    private static InvalidDataException Invalid(string message, Exception innerException) =>
        new(message, innerException);

    private readonly record struct XrefSection(
        PdfDictionary Trailer,
        IReadOnlyDictionary<int, PdfXrefEntry> Entries);
}
