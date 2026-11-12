using System.Text;

namespace MIPLabelCustomCheck.Internal.Pdf;

/// <summary>Result of <see cref="PdfLexer.ReadIndirectObject"/>.</summary>
internal readonly record struct PdfIndirectObject(int ObjectNumber, int Generation, PdfValue Value);

/// <summary>
/// A hand-rolled tokenizer/parser for PDF COS syntax (ISO 32000-1 clause 7.3), operating over a
/// single bounded, in-memory byte slice supplied by the caller (never the whole file). It never
/// searches arbitrary document text: every entry point either parses one self-contained value, or
/// one indirect object terminated by its own <c>endobj</c>.
/// </summary>
internal sealed class PdfLexer
{
    internal const int MaxNestingDepth = 64;
    internal const int MaxCollectionItems = 100_000;
    internal const int MaxNameBytes = 1024;
    internal const int MaxStringBytes = 4 * 1024 * 1024;
    internal const int MaxStreamBytes = 16 * 1024 * 1024;

    private readonly ReadOnlyMemory<byte> _buffer;
    private int _position;

    internal PdfLexer(ReadOnlyMemory<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>The current cursor position within the buffer supplied to the constructor.</summary>
    internal int Position => _position;

    /// <summary>Reads exactly one self-terminating PDF value starting at the current position.</summary>
    internal PdfValue ReadValue() => ReadValue(depth: 0);

    /// <summary>
    /// Reads an "N G obj ... endobj" wrapper (optionally containing "stream ... endstream" raw
    /// bytes sized by the dictionary's direct integer <c>/Length</c>) starting at the current
    /// position.
    /// </summary>
    internal PdfIndirectObject ReadIndirectObject()
    {
        SkipWhitespaceAndComments();
        var objectNumber = ReadIntegerToken();
        SkipWhitespaceAndComments();
        var generation = ReadIntegerToken();
        ExpectKeyword("obj");

        if (objectNumber is < 0 or > int.MaxValue || generation is < 0 or > int.MaxValue)
        {
            throw Invalid("A PDF indirect object header has an out-of-range object or generation number.");
        }

        var value = ReadValue();

        if (value is PdfDictionary dictionary && TryExpectKeyword("stream"))
        {
            ConsumeStreamEndOfLine();
            var length = GetStreamLength(dictionary);
            var data = ReadRawBytes(length);
            SkipWhitespaceAndComments();
            ExpectKeyword("endstream");
            ExpectKeyword("endobj");
            return new PdfIndirectObject((int)objectNumber, (int)generation, new PdfStream(dictionary, data));
        }

        ExpectKeyword("endobj");
        return new PdfIndirectObject((int)objectNumber, (int)generation, value);
    }

    internal static bool IsWhitespace(byte value) =>
        value is 0x00 or 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

    internal static bool IsDelimiter(byte value) =>
        value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or
            (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private void SkipWhitespaceAndComments() => _position = SkipWhitespaceAndComments(_buffer.Span, _position);

    internal static int SkipWhitespaceAndComments(ReadOnlySpan<byte> data, int position)
    {
        while (position < data.Length)
        {
            var value = data[position];
            if (IsWhitespace(value))
            {
                position++;
                continue;
            }

            if (value == (byte)'%')
            {
                position++;
                while (position < data.Length && data[position] != (byte)'\r' && data[position] != (byte)'\n')
                {
                    position++;
                }

                continue;
            }

            break;
        }

        return position;
    }

    internal static bool TryMatchKeyword(ReadOnlySpan<byte> data, int position, string keyword, out int newPosition)
    {
        if (position + keyword.Length > data.Length)
        {
            newPosition = position;
            return false;
        }

        for (var index = 0; index < keyword.Length; index++)
        {
            if (data[position + index] != (byte)keyword[index])
            {
                newPosition = position;
                return false;
            }
        }

        var after = position + keyword.Length;
        if (after < data.Length && !IsWhitespace(data[after]) && !IsDelimiter(data[after]))
        {
            newPosition = position;
            return false;
        }

        newPosition = after;
        return true;
    }

    private PdfValue ReadValue(int depth)
    {
        if (depth > MaxNestingDepth)
        {
            throw Invalid("A PDF value's nesting exceeds the configured depth limit.");
        }

        SkipWhitespaceAndComments();
        var current = PeekByte();

        if (current == (byte)'/')
        {
            return ReadName();
        }

        if (current == (byte)'(')
        {
            return ReadLiteralString();
        }

        if (current == (byte)'<')
        {
            return PeekAtOffset(1) == '<' ? ReadDictionary(depth) : ReadHexString();
        }

        if (current == (byte)'[')
        {
            return ReadArray(depth);
        }

        if (current == (byte)'t')
        {
            ExpectKeyword("true");
            return new PdfBoolean(true);
        }

        if (current == (byte)'f')
        {
            ExpectKeyword("false");
            return new PdfBoolean(false);
        }

        if (current == (byte)'n')
        {
            ExpectKeyword("null");
            return new PdfNull();
        }

        if (current == (byte)'+' || current == (byte)'-' || current == (byte)'.' || IsDigit(current))
        {
            return ReadNumberOrReference();
        }

        throw Invalid($"Unexpected byte 0x{current:X2} while reading a PDF value.");
    }

    private PdfName ReadName()
    {
        Consume(1);
        var bytes = new List<byte>();

        while (!AtEnd())
        {
            var peek = PeekByte();
            if (IsWhitespace(peek) || IsDelimiter(peek))
            {
                break;
            }

            ReadByte();
            if (peek == (byte)'#')
            {
                if (AtEnd() || !IsHexDigit(PeekByte()))
                {
                    throw Invalid("A PDF name has an incomplete #xx escape.");
                }

                var high = ReadByte();
                if (AtEnd() || !IsHexDigit(PeekByte()))
                {
                    throw Invalid("A PDF name has an incomplete #xx escape.");
                }

                var low = ReadByte();
                bytes.Add((byte)((HexValue(high) << 4) | HexValue(low)));
            }
            else
            {
                bytes.Add(peek);
            }

            if (bytes.Count > MaxNameBytes)
            {
                throw Invalid("A PDF name exceeds the configured byte limit.");
            }
        }

        return new PdfName(Encoding.UTF8.GetString(bytes.ToArray()));
    }

    private PdfString ReadLiteralString()
    {
        Consume(1);
        var bytes = new List<byte>();
        var depth = 1;

        while (true)
        {
            if (AtEnd())
            {
                throw Invalid("A PDF literal string is not terminated.");
            }

            var value = ReadByte();
            switch (value)
            {
                case (byte)'(':
                    depth++;
                    bytes.Add(value);
                    break;
                case (byte)')':
                    depth--;
                    if (depth == 0)
                    {
                        return new PdfString(bytes.ToArray());
                    }

                    bytes.Add(value);
                    break;
                case (byte)'\\':
                    ReadStringEscape(bytes);
                    break;
                case (byte)'\r':
                    if (!AtEnd() && PeekByte() == '\n')
                    {
                        ReadByte();
                    }

                    bytes.Add((byte)'\n');
                    break;
                default:
                    bytes.Add(value);
                    break;
            }

            InputGuard.CheckedLength(bytes.Count, MaxStringBytes, "A PDF literal string");
        }
    }

    private void ReadStringEscape(List<byte> bytes)
    {
        if (AtEnd())
        {
            throw Invalid("A PDF literal string escape is not terminated.");
        }

        var value = ReadByte();
        switch (value)
        {
            case (byte)'n':
                bytes.Add((byte)'\n');
                break;
            case (byte)'r':
                bytes.Add((byte)'\r');
                break;
            case (byte)'t':
                bytes.Add((byte)'\t');
                break;
            case (byte)'b':
                bytes.Add(0x08);
                break;
            case (byte)'f':
                bytes.Add(0x0C);
                break;
            case (byte)'(':
                bytes.Add((byte)'(');
                break;
            case (byte)')':
                bytes.Add((byte)')');
                break;
            case (byte)'\\':
                bytes.Add((byte)'\\');
                break;
            case (byte)'\r':
                if (!AtEnd() && PeekByte() == '\n')
                {
                    ReadByte();
                }

                break;
            case (byte)'\n':
                break;
            case >= (byte)'0' and <= (byte)'7':
            {
                var octalValue = value - (byte)'0';
                for (var index = 0; index < 2 && !AtEnd() && PeekByte() is >= (byte)'0' and <= (byte)'7'; index++)
                {
                    octalValue = (octalValue * 8) + (ReadByte() - (byte)'0');
                }

                bytes.Add((byte)(octalValue & 0xFF));
                break;
            }

            default:
                bytes.Add(value);
                break;
        }
    }

    private PdfString ReadHexString()
    {
        Consume(1);
        var bytes = new List<byte>();
        int? highNibble = null;

        while (true)
        {
            if (AtEnd())
            {
                throw Invalid("A PDF hex string is not terminated.");
            }

            var value = ReadByte();
            if (value == (byte)'>')
            {
                break;
            }

            if (IsWhitespace(value))
            {
                continue;
            }

            if (!IsHexDigit(value))
            {
                throw Invalid("A PDF hex string contains a non-hex-digit character.");
            }

            if (highNibble is null)
            {
                highNibble = HexValue(value);
            }
            else
            {
                bytes.Add((byte)((highNibble.Value << 4) | HexValue(value)));
                highNibble = null;
            }

            InputGuard.CheckedLength(bytes.Count, MaxStringBytes, "A PDF hex string");
        }

        if (highNibble is { } remaining)
        {
            bytes.Add((byte)(remaining << 4));
        }

        return new PdfString(bytes.ToArray());
    }

    private PdfArray ReadArray(int depth)
    {
        Consume(1);
        var items = new List<PdfValue>();

        while (true)
        {
            SkipWhitespaceAndComments();
            if (!AtEnd() && PeekByte() == ']')
            {
                Consume(1);
                break;
            }

            items.Add(ReadValue(depth + 1));
            if (items.Count > MaxCollectionItems)
            {
                throw Invalid("A PDF array exceeds the configured item limit.");
            }
        }

        return new PdfArray(items);
    }

    private PdfDictionary ReadDictionary(int depth)
    {
        Consume(2);
        var items = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        var count = 0;

        while (true)
        {
            SkipWhitespaceAndComments();
            if (TryMatchLiteral(">>"))
            {
                break;
            }

            if (AtEnd() || PeekByte() != '/')
            {
                throw Invalid("A PDF dictionary key must be a name.");
            }

            var key = ReadName().Value;
            var value = ReadValue(depth + 1);
            items[key] = value;

            count++;
            if (count > MaxCollectionItems)
            {
                throw Invalid("A PDF dictionary exceeds the configured item limit.");
            }
        }

        return new PdfDictionary(items);
    }

    private bool TryMatchLiteral(string literal)
    {
        var span = _buffer.Span;
        if (_position + literal.Length > span.Length)
        {
            return false;
        }

        for (var index = 0; index < literal.Length; index++)
        {
            if (span[_position + index] != (byte)literal[index])
            {
                return false;
            }
        }

        _position += literal.Length;
        return true;
    }

    private PdfValue ReadNumberOrReference()
    {
        var first = ReadIntegerToken();
        var posAfterFirst = _position;

        SkipWhitespaceAndComments();
        if (!AtEnd() && (PeekByte() == '+' || PeekByte() == '-' || IsDigit(PeekByte())))
        {
            var second = ReadIntegerToken();
            SkipWhitespaceAndComments();
            if (!AtEnd() && PeekByte() == 'R')
            {
                var afterR = PeekAtOffset(1);
                if (afterR == -1 || IsWhitespace((byte)afterR) || IsDelimiter((byte)afterR))
                {
                    ReadByte();
                    if (first is < 0 or > int.MaxValue || second is < 0 or > int.MaxValue)
                    {
                        throw Invalid(
                            "A PDF indirect reference has an out-of-range object or generation number.");
                    }

                    return new PdfReference((int)first, (int)second);
                }
            }
        }

        _position = posAfterFirst;
        return new PdfNumber(first);
    }

    private long ReadIntegerToken()
    {
        var negative = false;
        if (!AtEnd() && (PeekByte() == '+' || PeekByte() == '-'))
        {
            negative = ReadByte() == (byte)'-';
        }

        var digitsStart = _position;
        while (!AtEnd() && IsDigit(PeekByte()))
        {
            ReadByte();
        }

        if (_position == digitsStart)
        {
            if (!AtEnd() && PeekByte() == '.')
            {
                throw new PdfUnsupportedFeatureException("PDF real numbers are not supported by this reader.");
            }

            throw Invalid("A PDF number is missing digits.");
        }

        if (!AtEnd() && PeekByte() == '.')
        {
            throw new PdfUnsupportedFeatureException("PDF real numbers are not supported by this reader.");
        }

        if (!TryParseDigitsAsLong(_buffer.Span[digitsStart.._position], negative, out var value))
        {
            throw Invalid("A PDF number is out of the supported range.");
        }

        return value;
    }

    private static bool TryParseDigitsAsLong(ReadOnlySpan<byte> digits, bool negative, out long value)
    {
        value = 0;
        foreach (var digit in digits)
        {
            var digitValue = digit - (byte)'0';
            if (value > (long.MaxValue - digitValue) / 10)
            {
                return false;
            }

            value = (value * 10) + digitValue;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    private void ExpectKeyword(string keyword)
    {
        if (!TryExpectKeyword(keyword))
        {
            throw Invalid($"Expected the PDF keyword '{keyword}'.");
        }
    }

    private bool TryExpectKeyword(string keyword)
    {
        var saved = _position;
        SkipWhitespaceAndComments();
        if (TryMatchKeyword(_buffer.Span, _position, keyword, out var newPosition))
        {
            _position = newPosition;
            return true;
        }

        _position = saved;
        return false;
    }

    private void ConsumeStreamEndOfLine()
    {
        if (!AtEnd() && PeekByte() == '\r')
        {
            ReadByte();
            if (AtEnd() || PeekByte() != '\n')
            {
                throw Invalid("The PDF 'stream' keyword must be followed by an end-of-line marker.");
            }

            ReadByte();
            return;
        }

        if (!AtEnd() && PeekByte() == '\n')
        {
            ReadByte();
            return;
        }

        throw Invalid("The PDF 'stream' keyword must be followed by an end-of-line marker.");
    }

    private static long GetStreamLength(PdfDictionary dictionary)
    {
        if (!dictionary.Items.TryGetValue("Length", out var lengthValue))
        {
            throw Invalid("A PDF stream dictionary has no /Length.");
        }

        if (lengthValue is PdfReference)
        {
            throw new PdfUnsupportedFeatureException(
                "An indirect /Length reference is not supported by this reader.");
        }

        if (lengthValue is not PdfNumber number)
        {
            throw Invalid("A PDF stream /Length must be a number.");
        }

        return number.Value;
    }

    private byte[] ReadRawBytes(long length)
    {
        var streamLength = InputGuard.CheckedLength(length, MaxStreamBytes, "A PDF stream's declared /Length");
        if (streamLength > _buffer.Length - _position)
        {
            throw Invalid("A PDF stream's declared /Length exceeds the available data.");
        }

        var data = _buffer.Span.Slice(_position, streamLength).ToArray();
        _position += streamLength;
        return data;
    }

    private bool AtEnd() => _position >= _buffer.Length;

    private byte PeekByte() =>
        AtEnd() ? throw Invalid("Unexpected end of PDF data.") : _buffer.Span[_position];

    private int PeekAtOffset(int offset)
    {
        var index = _position + offset;
        return index >= 0 && index < _buffer.Length ? _buffer.Span[index] : -1;
    }

    private byte ReadByte()
    {
        var value = PeekByte();
        _position++;
        return value;
    }

    private void Consume(int count) => _position += count;

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';

    private static bool IsHexDigit(byte value) =>
        IsDigit(value) || value is >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F';

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - (byte)'0',
        >= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
        >= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
        _ => throw Invalid("An expected hex digit is not a hex digit.")
    };

    private static InvalidDataException Invalid(string message) => new(message);
}
