namespace MIPLabelCustomCheck.Internal.Pdf;

internal abstract record PdfValue;

internal sealed record PdfName(string Value) : PdfValue;

internal sealed record PdfNumber(long Value) : PdfValue;

internal sealed record PdfBoolean(bool Value) : PdfValue;

internal sealed record PdfString(byte[] Bytes) : PdfValue;

internal sealed record PdfArray(IReadOnlyList<PdfValue> Items) : PdfValue;

internal sealed record PdfDictionary(IReadOnlyDictionary<string, PdfValue> Items) : PdfValue
{
    /// <summary>
    /// Looks up <paramref name="key"/> and reports whether it is present and holds an indirect
    /// reference. Values that are missing or that hold some other kind of <see cref="PdfValue"/>
    /// both return <see langword="false"/>; callers that need to distinguish "absent" from
    /// "present but not a reference" should inspect <see cref="Items"/> directly.
    /// </summary>
    internal bool TryGetReference(string key, out PdfReference reference)
    {
        if (Items.TryGetValue(key, out var value) && value is PdfReference found)
        {
            reference = found;
            return true;
        }

        reference = null!;
        return false;
    }
}

internal sealed record PdfReference(int ObjectNumber, int Generation) : PdfValue;

internal sealed record PdfStream(PdfDictionary Dictionary, byte[] Data) : PdfValue;

internal sealed record PdfNull : PdfValue;

/// <summary>
/// Signals that the parser recognized a PDF construct it deliberately does not implement (for
/// example, a stream filter other than FlateDecode, or an indirect <c>/Length</c> reference)
/// rather than a violation of PDF syntax. Callers should map this to an "unsupported"
/// classification, distinct from the "corrupt" classification used for
/// <see cref="InvalidDataException"/>.
/// </summary>
internal sealed class PdfUnsupportedFeatureException : Exception
{
    internal PdfUnsupportedFeatureException(string message)
        : base(message)
    {
    }
}
