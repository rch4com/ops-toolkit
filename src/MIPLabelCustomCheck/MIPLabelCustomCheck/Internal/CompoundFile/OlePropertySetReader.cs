using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MIPLabelCustomCheck.Internal.CompoundFile;

internal enum OlePropertySetStatus
{
    /// <summary>The user-defined properties FMTID section is not present in this stream.</summary>
    NotPresent,

    Success
}

internal sealed record OlePropertySetReadResult(
    OlePropertySetStatus Status,
    IReadOnlyList<KeyValuePair<string, string>> Properties)
{
    internal static readonly OlePropertySetReadResult NotPresent =
        new(OlePropertySetStatus.NotPresent, []);

    internal static OlePropertySetReadResult Success(IReadOnlyList<KeyValuePair<string, string>> properties) =>
        new(OlePropertySetStatus.Success, properties);
}

/// <summary>
/// Decodes the "user-defined properties" section (FMTID {D5CDD505-2E9C-101B-9397-08002B2CF9AE})
/// of an OLE property set stream (MS-OLEPS), such as a DocumentSummaryInformation stream, without
/// depending on any code-page-provider NuGet package.
/// </summary>
internal static class OlePropertySetReader
{
    private const int CodePageUtf16 = 1200;
    private const int CodePageUtf8 = 65001;
    private const int CodePageWindows1252 = 1252;

    private const uint VtI2 = 0x0002;
    private const uint VtI4 = 0x0003;
    private const uint VtBool = 0x000B;
    private const uint VtLpstr = 0x001E;
    private const uint VtLpwstr = 0x001F;
    private const uint VtFileTime = 0x0040;

    private const int HeaderSize = 28;
    private const int FmtidEntrySize = 20;
    private const int MaxPropertySets = 2;
    private const int MaxProperties = 8192;
    private const int MaxDictionaryEntries = 8192;

    private static readonly Guid UserDefinedPropertiesFmtid =
        new("D5CDD505-2E9C-101B-9397-08002B2CF9AE");

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly UnicodeEncoding StrictUnicode =
        new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    // Windows-1252 differs from Latin-1 only in the 0x80-0x9F range, where it defines
    // printable characters (the C1 control range is otherwise unused in practice).
    // .NET does not expose "Windows-1252" without the CodePagesEncodingProvider NuGet
    // package, so this is a small, explicit, hand-rolled mapping table for that range only;
    // 0x00-0x7F and 0xA0-0xFF map to the identical Unicode code point (Latin-1 passthrough).
    private static readonly string Windows1252HighRange =
        "€‚ƒ„…†‡" +
        "ˆ‰Š‹ŒŽ" +
        "‘’“”•–—" +
        "˜™š›œžŸ";

    internal static OlePropertySetReadResult ReadUserDefinedProperties(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length < HeaderSize)
        {
            throw Invalid("The property set header is truncated.");
        }

        if (ReadUInt16(bytes, 0) != 0xFFFE)
        {
            throw Invalid("The property set byte order is invalid.");
        }

        var numPropertySets = ReadUInt32(bytes, 24);
        if (numPropertySets is 0 or > MaxPropertySets)
        {
            throw Invalid("The property set count is unsupported.");
        }

        var seenFmtids = new HashSet<Guid>();
        var directoryOffset = HeaderSize;
        int? sectionOffset = null;

        for (var index = 0; index < numPropertySets; index++)
        {
            if (directoryOffset > bytes.Length - FmtidEntrySize)
            {
                throw Invalid("The property set FMTID directory is truncated.");
            }

            var fmtid = new Guid(bytes.AsSpan(directoryOffset, 16));
            var offset = ReadUInt32(bytes, directoryOffset + 16);

            if (!seenFmtids.Add(fmtid))
            {
                throw Invalid("The property set contains a duplicate FMTID.");
            }

            if (fmtid == UserDefinedPropertiesFmtid)
            {
                if (offset > bytes.Length - 8)
                {
                    throw Invalid("The property set section offset is invalid.");
                }

                sectionOffset = checked((int)offset);
            }

            directoryOffset += FmtidEntrySize;
        }

        return sectionOffset is { } secOffset
            ? ReadSection(bytes, secOffset)
            : OlePropertySetReadResult.NotPresent;
    }

    private static OlePropertySetReadResult ReadSection(byte[] bytes, int secOffset)
    {
        var size = ReadUInt32(bytes, secOffset);
        var numProperties = ReadUInt32(bytes, secOffset + 4);

        if (size < 8 || size > checked((uint)(bytes.Length - secOffset)))
        {
            throw Invalid("The property set section size is invalid.");
        }

        var sectionEnd = checked(secOffset + (int)size);

        if (numProperties > MaxProperties || checked(8L + ((long)numProperties * 8)) > size)
        {
            throw Invalid("The property set property count is invalid.");
        }

        var directory = new (uint Id, uint Offset)[numProperties];
        var seenIds = new HashSet<uint>();
        var propertyOffset = secOffset + 8;

        for (var i = 0; i < numProperties; i++)
        {
            var id = ReadUInt32(bytes, propertyOffset);
            var offset = ReadUInt32(bytes, propertyOffset + 4);

            if (!seenIds.Add(id))
            {
                throw Invalid("The property set contains a duplicate property identifier.");
            }

            if (offset < 8 || offset > size - 4)
            {
                throw Invalid("The property set property offset is invalid.");
            }

            directory[i] = (id, offset);
            propertyOffset += 8;
        }

        if (!TryReadCodePage(bytes, secOffset, sectionEnd, directory, out var codePage))
        {
            throw Invalid("The property set section has no code page property.");
        }

        Dictionary<uint, string>? names = null;
        foreach (var (id, offset) in directory)
        {
            if (id != 0)
            {
                continue;
            }

            var absOffset = checked(secOffset + (int)offset);
            names = ReadDictionary(bytes, absOffset, sectionEnd, codePage);
            break;
        }

        names ??= [];

        var properties = new List<KeyValuePair<string, string>>();
        foreach (var (id, offset) in directory)
        {
            if (id is 0 or 1)
            {
                continue;
            }

            if (!names.TryGetValue(id, out var name))
            {
                // No dictionary name at all (never declared, or its name required a code
                // page we don't recognize and was skipped in ReadDictionary below) - the
                // property can't be surfaced without a name, so it is skipped, not fatal.
                continue;
            }

            var absOffset = checked(secOffset + (int)offset);
            var outcome = TryReadValue(bytes, absOffset, sectionEnd, codePage, out var value);
            if (outcome == ValueDecodeOutcome.Success)
            {
                properties.Add(new KeyValuePair<string, string>(name, value));
            }

            // ValueDecodeOutcome.Skip (an unrecognized VT_ type) and
            // ValueDecodeOutcome.CodePageUnsupported (a value that requires a code page we
            // don't recognize to decode) are both treated the same way: this one property is
            // left out, but every other already-decoded property - and the section as a
            // whole - still stands. A single unreadable property must never discard the rest.
        }

        return OlePropertySetReadResult.Success(properties);
    }

    private static bool TryReadCodePage(
        byte[] bytes,
        int secOffset,
        int sectionEnd,
        (uint Id, uint Offset)[] directory,
        out int codePage)
    {
        foreach (var (id, offset) in directory)
        {
            if (id != 1)
            {
                continue;
            }

            var absOffset = checked(secOffset + (int)offset);
            RequireBytes(absOffset + 8, sectionEnd);

            if (ReadUInt32(bytes, absOffset) != VtI2)
            {
                throw Invalid("The property set code page property has an unexpected type.");
            }

            if (ReadUInt16(bytes, absOffset + 6) != 0)
            {
                throw Invalid("The property set code page property padding is not zeroed.");
            }

            // The code page property is declared VT_I2 (signed), but Windows code page
            // identifiers are conceptually unsigned 16-bit numbers; code pages above 32767
            // (such as 65001, UTF-8) are stored using their two's-complement negative
            // representation and must be reinterpreted as unsigned here.
            var signedCodePage = ReadInt16(bytes, absOffset + 4);
            codePage = signedCodePage < 0 ? signedCodePage + 0x10000 : signedCodePage;
            return true;
        }

        codePage = 0;
        return false;
    }

    private enum ValueDecodeOutcome
    {
        Skip,
        CodePageUnsupported,
        Success
    }

    private static ValueDecodeOutcome TryReadValue(
        byte[] bytes,
        int absOffset,
        int sectionEnd,
        int codePage,
        out string value)
    {
        value = "";
        RequireBytes(absOffset + 4, sectionEnd);
        var type = ReadUInt32(bytes, absOffset);

        switch (type)
        {
            case VtI2:
                RequireBytes(absOffset + 8, sectionEnd);
                if (ReadUInt16(bytes, absOffset + 6) != 0)
                {
                    throw Invalid("The property set value padding is not zeroed.");
                }

                value = ReadInt16(bytes, absOffset + 4).ToString(CultureInfo.InvariantCulture);
                return ValueDecodeOutcome.Success;

            case VtI4:
                RequireBytes(absOffset + 8, sectionEnd);
                value = ReadInt32(bytes, absOffset + 4).ToString(CultureInfo.InvariantCulture);
                return ValueDecodeOutcome.Success;

            case VtBool:
                RequireBytes(absOffset + 8, sectionEnd);
                if (ReadUInt16(bytes, absOffset + 6) != 0)
                {
                    throw Invalid("The property set value padding is not zeroed.");
                }

                value = ReadInt16(bytes, absOffset + 4) != 0 ? "true" : "false";
                return ValueDecodeOutcome.Success;

            case VtLpstr:
                return TryReadCodePageString(bytes, absOffset + 4, sectionEnd, codePage, out value)
                    ? ValueDecodeOutcome.Success
                    : ValueDecodeOutcome.CodePageUnsupported;

            case VtLpwstr:
                value = ReadUnicodeString(bytes, absOffset + 4, sectionEnd);
                return ValueDecodeOutcome.Success;

            case VtFileTime:
                RequireBytes(absOffset + 12, sectionEnd);
                value = ReadFileTime(bytes, absOffset + 4);
                return ValueDecodeOutcome.Success;

            default:
                return ValueDecodeOutcome.Skip;
        }
    }

    private static Dictionary<uint, string> ReadDictionary(
        byte[] bytes,
        int absOffset,
        int sectionEnd,
        int codePage)
    {
        var names = new Dictionary<uint, string>();
        RequireBytes(absOffset + 4, sectionEnd);
        var count = ReadUInt32(bytes, absOffset);
        if (count > MaxDictionaryEntries)
        {
            throw Invalid("The property set dictionary has too many entries.");
        }

        var isUnicode = codePage == CodePageUtf16;
        var pos = absOffset + 4;

        for (var i = 0; i < count; i++)
        {
            RequireBytes(pos + 8, sectionEnd);
            var id = ReadUInt32(bytes, pos);
            var length = ReadUInt32(bytes, pos + 4);
            if (length == 0)
            {
                throw Invalid("The property set dictionary entry length is invalid.");
            }

            var contentStart = pos + 8;
            var byteLength = isUnicode ? checked((long)length * 2) : length;
            if (byteLength > sectionEnd - contentStart)
            {
                throw Invalid("The property set dictionary entry exceeds the section bounds.");
            }

            var contentLength = checked((int)byteLength);
            var content = bytes.AsSpan(contentStart, contentLength);

            // A name that requires a code page we don't recognize to decode is skipped, not
            // fatal to the whole dictionary: we still advance past its bytes correctly (so
            // every later entry is still located and read), we just leave this one id
            // unnamed. It will simply be invisible to the caller, exactly like a property
            // with an unrecognized VT_ type - one bad entry must not discard the rest.
            string? name;
            if (isUnicode)
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(content[^2..]) != 0)
                {
                    throw Invalid("The property set dictionary entry is not null terminated.");
                }

                name = DecodeStrictUnicode(content[..^2]);
            }
            else
            {
                if (content[^1] != 0)
                {
                    throw Invalid("The property set dictionary entry is not null terminated.");
                }

                name = TryDecodeSingleByteString(content[..^1], codePage, out var decoded)
                    ? decoded
                    : null;
            }

            if (name is not null && !names.TryAdd(id, name))
            {
                throw Invalid("The property set dictionary contains a duplicate property identifier.");
            }

            pos = contentStart + contentLength;
            if (isUnicode)
            {
                pos += (4 - (contentLength % 4)) % 4;
            }
        }

        return names;
    }

    private static bool TryReadCodePageString(
        byte[] bytes,
        int valueOffset,
        int sectionEnd,
        int codePage,
        out string value)
    {
        value = "";
        RequireBytes(valueOffset + 4, sectionEnd);
        var length = ReadUInt32(bytes, valueOffset);
        if (length == 0)
        {
            throw Invalid("The property set string length is invalid.");
        }

        var isUnicode = codePage == CodePageUtf16;
        var contentStart = valueOffset + 4;
        var byteLength = isUnicode ? checked((long)length * 2) : length;
        if (byteLength > sectionEnd - contentStart)
        {
            throw Invalid("The property set string exceeds the section bounds.");
        }

        var contentLength = checked((int)byteLength);
        var content = bytes.AsSpan(contentStart, contentLength);

        if (isUnicode)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(content[^2..]) != 0)
            {
                throw Invalid("The property set string is not null terminated.");
            }

            value = DecodeStrictUnicode(content[..^2]);
            return true;
        }

        if (content[^1] != 0)
        {
            throw Invalid("The property set string is not null terminated.");
        }

        return TryDecodeSingleByteString(content[..^1], codePage, out value);
    }

    private static string ReadUnicodeString(byte[] bytes, int valueOffset, int sectionEnd)
    {
        RequireBytes(valueOffset + 4, sectionEnd);
        var length = ReadUInt32(bytes, valueOffset);
        if (length == 0)
        {
            throw Invalid("The property set string length is invalid.");
        }

        var contentStart = valueOffset + 4;
        var byteLength = checked((long)length * 2);
        if (byteLength > sectionEnd - contentStart)
        {
            throw Invalid("The property set string exceeds the section bounds.");
        }

        var contentLength = checked((int)byteLength);
        var content = bytes.AsSpan(contentStart, contentLength);
        if (BinaryPrimitives.ReadUInt16LittleEndian(content[^2..]) != 0)
        {
            throw Invalid("The property set string is not null terminated.");
        }

        return DecodeStrictUnicode(content[..^2]);
    }

    private static string ReadFileTime(byte[] bytes, int valueOffset)
    {
        var low = ReadUInt32(bytes, valueOffset);
        var high = ReadUInt32(bytes, valueOffset + 4);
        var fileTime = unchecked((long)(((ulong)high << 32) | low));

        if (fileTime < 0)
        {
            throw Invalid("The property set FILETIME value is out of range.");
        }

        try
        {
            return DateTime.FromFileTimeUtc(fileTime).ToString("O", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw Invalid("The property set FILETIME value is out of range.", exception);
        }
    }

    private static string DecodeStrictUnicode(ReadOnlySpan<byte> content)
    {
        try
        {
            return StrictUnicode.GetString(content);
        }
        catch (DecoderFallbackException exception)
        {
            throw Invalid("The property set string is not valid UTF-16.", exception);
        }
    }

    private static bool TryDecodeSingleByteString(ReadOnlySpan<byte> content, int codePage, out string value)
    {
        switch (codePage)
        {
            case CodePageUtf8:
                try
                {
                    value = StrictUtf8.GetString(content);
                    return true;
                }
                catch (DecoderFallbackException exception)
                {
                    throw Invalid("The property set string is not valid UTF-8.", exception);
                }

            case CodePageWindows1252:
                value = DecodeWindows1252(content);
                return true;

            default:
                // The code page is not one of the three explicitly supported values. Rather
                // than guess at an unfamiliar single/double-byte code page's mapping, only
                // decode content that is unambiguous: bytes in the 0x00-0x7F range have the
                // identical meaning (US-ASCII) under virtually every Windows code page,
                // including this one. Any byte outside that range means genuinely decoding
                // this code page would require a table we do not have, so it is reported as
                // an unsupported (not invalid) variant instead of guessing.
                foreach (var b in content)
                {
                    if (b > 0x7F)
                    {
                        value = "";
                        return false;
                    }
                }

                value = Encoding.ASCII.GetString(content);
                return true;
        }
    }

    private static string DecodeWindows1252(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i] = b is >= 0x80 and <= 0x9F ? Windows1252HighRange[b - 0x80] : (char)b;
        }

        return new string(chars);
    }

    private static void RequireBytes(int endOffsetExclusive, int sectionEnd)
    {
        if (endOffsetExclusive > sectionEnd)
        {
            throw Invalid("The property set value exceeds the section bounds.");
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));

    private static short ReadInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(offset, sizeof(short)));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));

    private static InvalidDataException Invalid(string message) => new(message);

    private static InvalidDataException Invalid(string message, Exception innerException) =>
        new(message, innerException);
}
