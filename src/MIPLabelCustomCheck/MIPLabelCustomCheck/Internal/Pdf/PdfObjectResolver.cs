namespace MIPLabelCustomCheck.Internal.Pdf;

/// <summary>
/// Lazily resolves indirect PDF objects located through a <see cref="PdfCrossReferenceTable"/>,
/// decoding <c>/ObjStm</c> object streams for compressed (type-2) entries on demand. Resolved
/// objects are cached in a private, per-instance dictionary — never a static or shared cache — so
/// each <see cref="Open"/> call is independent and safe to use concurrently with any other.
/// </summary>
internal sealed class PdfObjectResolver
{
    private readonly Stream _stream;
    private readonly IReadOnlyDictionary<int, PdfXrefEntry> _entries;

    // Keyed by ObjectNumber only - Generation is ignored. That's fine here because the resolver
    // reads a single xref snapshot (the merged result of following /Prev), where each object
    // number maps to exactly one live entry; there's no scenario in which two different
    // generations of the same object number both need resolving within one resolver instance.
    private readonly Dictionary<int, PdfValue> _cache = [];

    private PdfObjectResolver(Stream stream, PdfCrossReferenceTable table)
    {
        _stream = stream;
        _entries = table.Entries;
        LatestTrailer = table.Trailer;
    }

    /// <summary>The trailer dictionary of the newest revision found by following /Prev.</summary>
    internal PdfDictionary LatestTrailer { get; }

    /// <summary>
    /// Locates the newest cross-reference revision chain in <paramref name="stream"/>. The stream
    /// must remain open and seekable for the lifetime of the returned resolver; ownership stays
    /// with the caller.
    /// </summary>
    internal static PdfObjectResolver Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The PDF stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new NotSupportedException("The PDF stream must be seekable.");
        }

        var table = PdfCrossReferenceReader.Read(stream);
        return new PdfObjectResolver(stream, table);
    }

    /// <summary>Resolves an indirect reference to its <see cref="PdfValue"/>, using the per-instance cache.</summary>
    internal PdfValue Resolve(PdfReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (_cache.TryGetValue(reference.ObjectNumber, out var cached))
        {
            return cached;
        }

        if (!_entries.TryGetValue(reference.ObjectNumber, out var entry))
        {
            throw Invalid("The requested PDF object has no cross-reference entry.");
        }

        PdfValue value;
        try
        {
            value = entry.Type switch
            {
                PdfXrefEntryType.InUse => ResolveDirect(reference, entry),
                PdfXrefEntryType.Compressed => ResolveCompressed(reference, entry),
                _ => throw Invalid("The requested PDF object's cross-reference entry is free.")
            };
        }
        catch (OverflowException exception)
        {
            throw Invalid("The requested PDF object contains an overflowing size or offset.", exception);
        }

        _cache[reference.ObjectNumber] = value;
        return value;
    }

    private PdfValue ResolveDirect(PdfReference reference, PdfXrefEntry entry)
    {
        var offset = entry.Field2;
        if (offset < 0 || offset >= _stream.Length)
        {
            throw Invalid("A PDF object's cross-reference offset is outside the file.");
        }

        var sliceLength = (int)Math.Min(PdfCrossReferenceReader.MaxIndirectObjectSliceBytes, _stream.Length - offset);
        var buffer = new byte[sliceLength];
        PdfCrossReferenceReader.ReadExactlyAt(_stream, offset, buffer);

        var lexer = new PdfLexer(buffer);
        var indirectObject = lexer.ReadIndirectObject();
        if (indirectObject.ObjectNumber != reference.ObjectNumber)
        {
            throw Invalid("A PDF object's declared number does not match its cross-reference entry.");
        }

        return indirectObject.Value;
    }

    private PdfValue ResolveCompressed(PdfReference reference, PdfXrefEntry entry)
    {
        var container = Resolve(new PdfReference(checked((int)entry.Field2), 0));
        if (container is not PdfStream objectStream)
        {
            throw Invalid("A PDF /ObjStm cross-reference entry does not resolve to a stream.");
        }

        if (!objectStream.Dictionary.Items.TryGetValue("N", out var countValue) ||
            countValue is not PdfNumber countNumber ||
            !objectStream.Dictionary.Items.TryGetValue("First", out var firstValue) ||
            firstValue is not PdfNumber { Value: >= 0 } first)
        {
            throw Invalid("A PDF /ObjStm is missing /N or /First.");
        }

        var count = InputGuard.CheckedLength(countNumber.Value, PdfLexer.MaxCollectionItems, "A PDF /ObjStm /N");
        var decoded = PdfCrossReferenceReader.DecodeStreamData(objectStream);
        var firstOffset = InputGuard.CheckedLength(
            first.Value,
            decoded.Length,
            "A PDF /ObjStm /First offset");

        var headerLexer = new PdfLexer(decoded.AsMemory(0, firstOffset));
        var pairs = new (int ObjectNumber, int RelativeOffset)[count];
        for (var index = 0; index < count; index++)
        {
            if (headerLexer.ReadValue() is not PdfNumber { Value: >= 0 and <= int.MaxValue } objectNumber ||
                headerLexer.ReadValue() is not PdfNumber { Value: >= 0 and <= int.MaxValue } relativeOffset)
            {
                throw Invalid("A PDF /ObjStm header pair is invalid.");
            }

            pairs[index] = ((int)objectNumber.Value, (int)relativeOffset.Value);
        }

        if (entry.Field3 < 0 || entry.Field3 >= pairs.Length)
        {
            throw Invalid("A PDF /ObjStm cross-reference index is out of range.");
        }

        var (compressedObjectNumber, relativeDataOffset) = pairs[entry.Field3];
        if (compressedObjectNumber != reference.ObjectNumber)
        {
            throw Invalid("A PDF /ObjStm entry's object number does not match its cross-reference entry.");
        }

        var absoluteOffset = checked((long)first.Value + relativeDataOffset);
        var absoluteOffsetLength = InputGuard.CheckedLength(
            absoluteOffset,
            decoded.Length,
            "A PDF /ObjStm object offset");

        var valueLexer = new PdfLexer(decoded.AsMemory(absoluteOffsetLength));
        return valueLexer.ReadValue();
    }

    private static InvalidDataException Invalid(string message) => new(message);

    private static InvalidDataException Invalid(string message, Exception innerException) =>
        new(message, innerException);
}
