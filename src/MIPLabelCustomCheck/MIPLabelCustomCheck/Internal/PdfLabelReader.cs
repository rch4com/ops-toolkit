using System.Text;
using System.Xml;
using MIPLabelCustomCheck.Internal.Pdf;

namespace MIPLabelCustomCheck.Internal;

/// <summary>
/// Reads Microsoft Purview sensitivity labels from a PDF's Info dictionary string values and its
/// Root catalog's /Metadata XMP stream. Performs only the resolutions needed to reach those two
/// sources - trailer, /Encrypt presence, /Info, /Root, and /Root's /Metadata - and never scans the
/// document body for label keys. Structural corruption (<see cref="InvalidDataException"/>,
/// <see cref="XmlException"/>) maps to <see cref="LabelReadResult.Invalid"/>; a recognized but
/// deliberately unimplemented construct (<see cref="PdfUnsupportedFeatureException"/>) maps to
/// <see cref="LabelReadResult.Unsupported"/>.
/// </summary>
internal static class PdfLabelReader
{
    private const int MaxDecodedMetadataBytes = 4 * 1024 * 1024;

    private static readonly char[] PdfDocEncodingTable = BuildPdfDocEncodingTable();

    internal static LabelReadResult Read(Stream stream, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            return ReadCore(stream, includeDetails);
        }
        catch (PdfUnsupportedFeatureException)
        {
            return LabelReadResult.Unsupported();
        }
        catch (Exception exception) when (
            exception is InvalidDataException or XmlException or DecoderFallbackException)
        {
            return LabelReadResult.Invalid();
        }
    }

    private static LabelReadResult ReadCore(Stream stream, bool includeDetails)
    {
        var document = PdfObjectResolver.Open(stream);
        var trailer = document.LatestTrailer;
        var encrypted = trailer.ContainsKey("Encrypt");
        var protectionDetail = ReadProtectionDetail(document, trailer);

        var infoProperties = ReadInfoProperties(document, trailer.GetOptionalReference("Info"));
        var infoParsed = LabelPropertyParser.Parse(infoProperties, includeDetails);

        var catalog = document.ResolveDictionary(trailer.GetRequiredReference("Root"));
        var xmpParsed = ReadMetadataXmp(
            document, catalog.GetOptionalReference("Metadata"), encrypted, includeDetails);

        var parsed = Combine(infoParsed, xmpParsed);

        if (parsed.Result.InvalidMetadata)
        {
            return LabelReadResult.Invalid();
        }

        if (parsed.Result.LabelIds.Count > 0)
        {
            return LabelReadResult.Labeled(
                parsed.Result.LabelIds,
                encrypted ? ProtectionType.Native : ProtectionType.None,
                parsed.Result.Details,
                parsed.Sources,
                protectionDetail);
        }

        return encrypted
            ? LabelReadResult.ProtectedUnknown(ProtectionType.Native, protectionDetail)
            : LabelReadResult.Unlabeled();
    }

    private static string ReadProtectionDetail(PdfObjectResolver document, PdfDictionary trailer)
    {
        if (!trailer.Items.TryGetValue("Encrypt", out var encryptValue))
        {
            return string.Empty;
        }

        var parts = new List<string> { "PDF /Encrypt" };
        if (TryResolveEncryptDictionary(document, encryptValue, out var encryptDictionary))
        {
            AddNameDetail(parts, encryptDictionary, "Filter");
            AddNameDetail(parts, encryptDictionary, "SubFilter");
        }

        return string.Join(' ', parts);
    }

    private static bool TryResolveEncryptDictionary(
        PdfObjectResolver document,
        PdfValue encryptValue,
        out PdfDictionary encryptDictionary)
    {
        try
        {
            var resolved = encryptValue is PdfReference reference
                ? document.Resolve(reference)
                : encryptValue;

            if (resolved is PdfDictionary dictionary)
            {
                encryptDictionary = dictionary;
                return true;
            }
        }
        catch (Exception exception) when (
            exception is InvalidDataException or PdfUnsupportedFeatureException)
        {
        }

        encryptDictionary = null!;
        return false;
    }

    private static void AddNameDetail(List<string> parts, PdfDictionary dictionary, string key)
    {
        if (dictionary.Items.TryGetValue(key, out var value) && value is PdfName name)
        {
            parts.Add($"/{key} /{name.Value}");
        }
    }

    private static (LabelPropertyParseResult Result, IReadOnlyList<LabelSource> Sources) Combine(
        LabelPropertyParseResult first,
        LabelPropertyParseResult second)
    {
        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var sources = new List<LabelSource>();

        foreach (var labelId in first.LabelIds)
        {
            if (seenLabelIds.Add(labelId))
            {
                labelIds.Add(labelId);
                sources.Add(LabelSource.PdfInfoDictionary);
            }
        }

        foreach (var labelId in second.LabelIds)
        {
            if (seenLabelIds.Add(labelId))
            {
                labelIds.Add(labelId);
                sources.Add(LabelSource.Xmp);
            }
        }

        // 부가 정보도 레이블 ID와 같은 우선순위를 따른다: 같은 레이블이 Info와 XMP 양쪽에
        // 있으면 먼저 본 쪽(Info)의 값을 쓴다.
        var details = new List<LabelDetail>();
        var seenDetailIds = new HashSet<Guid>();

        foreach (var detail in first.Details.Concat(second.Details))
        {
            if (seenDetailIds.Add(detail.Id))
            {
                details.Add(detail);
            }
        }

        return (
            new LabelPropertyParseResult(
                labelIds,
                first.InvalidMetadata || second.InvalidMetadata,
                details),
            sources);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ReadInfoProperties(
        PdfObjectResolver document,
        PdfReference? infoReference)
    {
        if (infoReference is not { } reference)
        {
            return [];
        }

        var infoDictionary = document.ResolveDictionary(reference);
        var properties = new List<KeyValuePair<string, string>>();

        foreach (var (key, value) in infoDictionary.Items)
        {
            if (value is PdfString pdfString)
            {
                properties.Add(KeyValuePair.Create(key, DecodeInfoString(pdfString.Bytes)));
            }
        }

        return properties;
    }

    /// <summary>
    /// Resolves and parses the /Metadata XMP stream. This reader has no decryption capability, so
    /// when <paramref name="encrypted"/> is <see langword="true"/> it cannot distinguish "this
    /// stream is genuine ciphertext" from "this stream just doesn't decode/parse" - both manifest
    /// identically as a decode or XML-parse failure. Only those two stages - decoding the stream's
    /// raw bytes (an unsupported filter, or bytes that fail to inflate: the expected symptom of
    /// ciphertext flowing through FlateDecode) and parsing the result as XML (malformed markup:
    /// the expected symptom of ciphertext that happened to pass through an unfiltered or
    /// successfully-inflated stream) - are tolerated under encryption, contributing no label IDs
    /// without failing the overall read. Resolving the reference (a dangling/free xref entry, or a
    /// /Metadata value that isn't a stream) and the decoded-size limit are structural/security
    /// preconditions unrelated to encryption and always propagate, even when encrypted: if a
    /// stream decodes successfully well enough to exceed the byte cap, that is readable content
    /// violating a security bound, not ciphertext. When the document is not encrypted, every
    /// failure propagates so it can be classified as unsupported or invalid by the caller.
    /// </summary>
    private static LabelPropertyParseResult ReadMetadataXmp(
        PdfObjectResolver document,
        PdfReference? metadataReference,
        bool encrypted,
        bool includeDetails)
    {
        if (metadataReference is not { } reference)
        {
            return new LabelPropertyParseResult([], InvalidMetadata: false, []);
        }

        var metadataStream = ResolveMetadataStream(document, reference);

        if (!TryDecodeMetadataStreamData(metadataStream, encrypted, out var decoded))
        {
            return new LabelPropertyParseResult([], InvalidMetadata: false, []);
        }

        // Enforced after DecodeStreamData has already materialized the decoded bytes, so a
        // transient allocation up to PdfLexer.MaxStreamBytes (16 MiB) can occur before rejection.
        // That's bounded and intentional - the cap here guards what this reader keeps around and
        // parses as XML, not the peak memory of decoding itself.
        if (decoded.Length > MaxDecodedMetadataBytes)
        {
            throw new InvalidDataException("The decoded PDF /Metadata stream exceeds the byte limit.");
        }

        return TryParseMetadataXmp(decoded, encrypted, includeDetails, out var parsed)
            ? parsed
            : new LabelPropertyParseResult([], InvalidMetadata: false, []);
    }

    private static PdfStream ResolveMetadataStream(PdfObjectResolver document, PdfReference reference)
    {
        if (document.Resolve(reference) is not PdfStream metadataStream)
        {
            throw new InvalidDataException("The PDF /Metadata entry does not resolve to a stream.");
        }

        return metadataStream;
    }

    private static bool TryDecodeMetadataStreamData(
        PdfStream metadataStream,
        bool encrypted,
        out byte[] decoded)
    {
        if (!encrypted)
        {
            decoded = PdfCrossReferenceReader.DecodeStreamData(metadataStream);
            return true;
        }

        try
        {
            decoded = PdfCrossReferenceReader.DecodeStreamData(metadataStream);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or PdfUnsupportedFeatureException)
        {
            decoded = [];
            return false;
        }
    }

    private static bool TryParseMetadataXmp(
        byte[] decoded,
        bool encrypted,
        bool includeDetails,
        out LabelPropertyParseResult parsed)
    {
        if (!encrypted)
        {
            parsed = ParseMetadataXmpCore(decoded, includeDetails);
            return true;
        }

        try
        {
            parsed = ParseMetadataXmpCore(decoded, includeDetails);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or XmlException or DecoderFallbackException)
        {
            parsed = new LabelPropertyParseResult([], InvalidMetadata: false, []);
            return false;
        }
    }

    private static LabelPropertyParseResult ParseMetadataXmpCore(byte[] decoded, bool includeDetails)
    {
        using var metadataMemoryStream = new MemoryStream(decoded, writable: false);
        return XmpLabelReader.Read(metadataMemoryStream, Math.Max(decoded.Length, 1), includeDetails);
    }

    /// <summary>
    /// Decodes a PDF Info dictionary string value: a UTF-16BE or UTF-16LE byte-order mark selects
    /// that encoding for the remaining bytes; otherwise the bytes are PDFDocEncoding (a superset of
    /// ASCII for the printable range).
    /// </summary>
    private static string DecodeInfoString(byte[] bytes)
    {
        if (HasBom(bytes, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (HasBom(bytes, 0xFF, 0xFE))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return DecodePdfDocEncoding(bytes);
    }

    private static bool HasBom(byte[] bytes, byte first, byte second) =>
        bytes.Length >= 2 && bytes[0] == first && bytes[1] == second;

    private static string DecodePdfDocEncoding(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (var index = 0; index < bytes.Length; index++)
        {
            chars[index] = PdfDocEncodingTable[bytes[index]];
        }

        return new string(chars);
    }

    /// <summary>
    /// Builds the PDFDocEncoding byte-to-Unicode table (ISO 32000-1:2008, Annex D, Table D.2).
    /// Bytes 0x20-0x7E and 0xA1-0xFF map to the same code point as ASCII/Latin-1; the remaining
    /// bytes substitute the small set of typographic characters the standard defines.
    /// </summary>
    private static char[] BuildPdfDocEncodingTable()
    {
        var table = new char[256];
        for (var value = 0; value < 256; value++)
        {
            table[value] = (char)value;
        }

        table[0x18] = '˘'; // breve
        table[0x19] = 'ˇ'; // caron
        table[0x1A] = 'ˆ'; // modifier letter circumflex accent
        table[0x1B] = '˙'; // dot above
        table[0x1C] = '˝'; // double acute accent
        table[0x1D] = '˛'; // ogonek
        table[0x1E] = '˚'; // ring above
        table[0x1F] = '˜'; // small tilde

        table[0x80] = '•'; // bullet
        table[0x81] = '†'; // dagger
        table[0x82] = '‡'; // double dagger
        table[0x83] = '…'; // horizontal ellipsis
        table[0x84] = '—'; // em dash
        table[0x85] = '–'; // en dash
        table[0x86] = 'ƒ'; // florin
        table[0x87] = '⁄'; // fraction slash
        table[0x88] = '‹'; // single left-pointing angle quotation mark
        table[0x89] = '›'; // single right-pointing angle quotation mark
        table[0x8A] = '−'; // minus sign
        table[0x8B] = '‰'; // per mille sign
        table[0x8C] = '„'; // double low-9 quotation mark
        table[0x8D] = '“'; // left double quotation mark
        table[0x8E] = '”'; // right double quotation mark
        table[0x8F] = '‘'; // left single quotation mark
        table[0x90] = '’'; // right single quotation mark
        table[0x91] = '‚'; // single low-9 quotation mark
        table[0x92] = '™'; // trademark sign
        table[0x93] = 'ﬁ'; // latin small ligature fi
        table[0x94] = 'ﬂ'; // latin small ligature fl
        table[0x95] = 'Ł'; // latin capital letter L with stroke
        table[0x96] = 'Œ'; // latin capital ligature OE
        table[0x97] = 'Š'; // latin capital letter S with caron
        table[0x98] = 'Ÿ'; // latin capital letter Y with diaeresis
        table[0x99] = 'Ž'; // latin capital letter Z with caron
        table[0x9A] = 'ı'; // latin small letter dotless i
        table[0x9B] = 'ł'; // latin small letter l with stroke
        table[0x9C] = 'œ'; // latin small ligature oe
        table[0x9D] = 'š'; // latin small letter s with caron
        table[0x9E] = 'ž'; // latin small letter z with caron
        table[0xA0] = '€'; // euro sign

        return table;
    }

    private static bool ContainsKey(this PdfDictionary dictionary, string key) =>
        dictionary.Items.ContainsKey(key);

    private static PdfReference? GetOptionalReference(this PdfDictionary dictionary, string key)
    {
        if (!dictionary.Items.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value is not PdfReference reference)
        {
            throw new InvalidDataException($"The PDF '/{key}' entry is not an indirect reference.");
        }

        return reference;
    }

    private static PdfReference GetRequiredReference(this PdfDictionary dictionary, string key) =>
        dictionary.GetOptionalReference(key) ??
        throw new InvalidDataException($"The PDF dictionary is missing the required '/{key}' entry.");

    private static PdfDictionary ResolveDictionary(this PdfObjectResolver document, PdfReference reference)
    {
        if (document.Resolve(reference) is not PdfDictionary dictionary)
        {
            throw new InvalidDataException("The resolved PDF object is not a dictionary.");
        }

        return dictionary;
    }
}
