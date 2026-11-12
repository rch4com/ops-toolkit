using System.IO.Compression;
using System.Text;
using MIPLabelCustomCheck.Internal.Pdf;

namespace MIPLabelCustomCheck.Tests.TestData;

/// <summary>
/// Builds minimal, byte-precise synthetic PDF documents for <c>PdfLexer</c>,
/// <c>PdfCrossReferenceReader</c>, and <c>PdfObjectResolver</c> tests. Every fixture tracks its
/// own byte offsets so that xref tables and streams point at exact, correct locations (or, for
/// the malformed fixtures, deliberately incorrect ones).
/// </summary>
internal static class PdfFixture
{
    /// <summary>A single object (the catalog) resolved via a classic xref table and trailer.</summary>
    internal static MemoryStream WithClassicXref()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        writer.WriteClassicXref(2, new Dictionary<int, long> { [1] = rootOffset }, "/Root 1 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// Two incremental revisions: object 1 (the catalog) is replaced in the newer revision, while
    /// object 2 exists only in the older revision and must be reached by following /Prev.
    /// </summary>
    internal static MemoryStream WithClassicXrefAndPrev()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();

        var rootOffsetV1 = writer.WriteObject(1, "<< /Type /Catalog /Marker (v1) >>");
        var obj2Offset = writer.WriteObject(2, "<< /OnlyInOldRevision true >>");
        var oldXrefOffset = writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffsetV1, [2] = obj2Offset },
            "/Root 1 0 R");

        var rootOffsetV2 = writer.WriteObject(1, "<< /Type /Catalog /Marker (v2) >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffsetV2 },
            $"/Root 1 0 R /Prev {oldXrefOffset}");

        return writer.ToStream();
    }

    /// <summary>
    /// A classic xref table whose entries fall into two disjoint subsections - "0 1" (only the
    /// mandatory free-list head) and "3 2" (the catalog and an info object) - because the
    /// referenced objects are not contiguous with object 0. Both subsections must be parsed for
    /// both objects to resolve.
    /// </summary>
    internal static MemoryStream WithClassicXrefTwoSubsections()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(3, "<< /Type /Catalog >>");
        var infoOffset = writer.WriteObject(4, "<< /Marker (second-subsection) >>");
        writer.WriteClassicXref(
            5,
            new Dictionary<int, long> { [3] = rootOffset, [4] = infoOffset },
            "/Root 3 0 R /Info 4 0 R");
        return writer.ToStream();
    }

    /// <summary>A cross-reference stream using /W and /Index, compressed with FlateDecode.</summary>
    internal static MemoryStream WithXrefStream()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var xrefStreamOffset = writer.Position;

        var entries = new Dictionary<int, (byte Type, long Field2, int Field3)>
        {
            [0] = (0, 0, 65535),
            [1] = (1, rootOffset, 0),
            [2] = (1, xrefStreamOffset, 0),
        };

        var xrefOffset = writer.WriteXrefStream(2, 3, entries, "/Root 1 0 R", useIndex: true);
        writer.WriteStartXref(xrefOffset);
        return writer.ToStream();
    }

    /// <summary>
    /// A cross-reference stream whose /Index declares two disjoint runs - "0 1" (only the
    /// mandatory free-list head) and "5 3" (the catalog, an info object, and the xref stream
    /// itself) - because the referenced objects are not contiguous with object 0. Both runs must
    /// be parsed for both the catalog and the info object to resolve.
    /// </summary>
    internal static MemoryStream WithXrefStreamTwoIndexRuns()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(5, "<< /Type /Catalog >>");
        var infoOffset = writer.WriteObject(6, "<< /Marker (second-run) >>");
        var xrefStreamOffset = writer.Position;

        var entries = new Dictionary<int, (byte Type, long Field2, int Field3)>
        {
            [0] = (0, 0, 65535),
            [5] = (1, rootOffset, 0),
            [6] = (1, infoOffset, 0),
            [7] = (1, xrefStreamOffset, 0),
        };

        var xrefOffset = writer.WriteXrefStream(
            7,
            8,
            entries,
            "/Root 5 0 R /Info 6 0 R",
            useIndex: true);
        writer.WriteStartXref(xrefOffset);
        return writer.ToStream();
    }

    /// <summary>
    /// A cross-reference stream whose /Filter is neither absent nor /FlateDecode, which must be
    /// classified as unsupported rather than corrupt.
    /// </summary>
    internal static MemoryStream WithUnsupportedXrefStreamFilter()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var xrefStreamOffset = writer.Position;

        var entries = new Dictionary<int, (byte Type, long Field2, int Field3)>
        {
            [0] = (0, 0, 65535),
            [1] = (1, rootOffset, 0),
            [2] = (1, xrefStreamOffset, 0),
        };

        var xrefOffset = writer.WriteXrefStream(
            2,
            3,
            entries,
            "/Root 1 0 R",
            useIndex: false,
            filterName: "DCTDecode");
        writer.WriteStartXref(xrefOffset);
        return writer.ToStream();
    }

    /// <summary>
    /// A type-2 (compressed) xref entry whose object lives inside an /ObjStm, reached through an
    /// xref stream.
    /// </summary>
    internal static MemoryStream WithObjectStream()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();

        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");

        var (objStmData, first) = BuildObjStmContent([(3, "<< /Kind (Compressed) >>")]);
        var compressed = Compress(objStmData);
        var objStmOffset = writer.WriteStreamObject(
            2,
            $"<< /Type /ObjStm /N 1 /First {first} /Filter /FlateDecode /Length {compressed.Length} >>",
            compressed);

        var xrefStreamOffset = writer.Position;
        var entries = new Dictionary<int, (byte Type, long Field2, int Field3)>
        {
            [0] = (0, 0, 65535),
            [1] = (1, rootOffset, 0),
            [2] = (1, objStmOffset, 0),
            [3] = (2, 2, 0),
            [4] = (1, xrefStreamOffset, 0),
        };

        var xrefOffset = writer.WriteXrefStream(4, 5, entries, "/Root 1 0 R", useIndex: false);
        writer.WriteStartXref(xrefOffset);
        return writer.ToStream();
    }

    /// <summary>The trailing <c>startxref</c> value points far beyond the end of the file.</summary>
    internal static MemoryStream WithInvalidStartXrefOffset()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        writer.WriteClassicXref(
            2,
            new Dictionary<int, long> { [1] = rootOffset },
            "/Root 1 0 R",
            startxrefOverride: 999_999_999);
        return writer.ToStream();
    }

    /// <summary>The single xref section's own trailer /Prev points back at itself.</summary>
    internal static MemoryStream WithCyclicPrev()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var xrefOffset = writer.Position;
        writer.WriteClassicXref(
            2,
            new Dictionary<int, long> { [1] = rootOffset },
            $"/Root 1 0 R /Prev {xrefOffset}");
        return writer.ToStream();
    }

    /// <summary>Object 2's xref entry declares a byte offset far beyond the end of the file.</summary>
    internal static MemoryStream WithOutOfRangeObjectOffset()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = 999_999_999 },
            "/Root 1 0 R");
        return writer.ToStream();
    }

    /// <summary>Object 2 is a stream whose declared /Length wildly exceeds the byte-limit cap.</summary>
    internal static MemoryStream WithOversizedStreamLength()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var streamOffset = writer.WriteStreamObject(
            2,
            "<< /Length 999999999 >>",
            "tiny"u8.ToArray());
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = streamOffset },
            "/Root 1 0 R");
        return writer.ToStream();
    }

    /// <summary>A Catalog plus an Info dictionary with one active MSIP label property, no Metadata.</summary>
    internal static MemoryStream WithInfoLabel(Guid labelId)
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var infoOffset = writer.WriteObject(2, $"<< /MSIP_Label_{labelId:D}_Enabled (true) >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = infoOffset },
            "/Root 1 0 R /Info 2 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// A Catalog referencing an unfiltered /Metadata XMP stream with one active MSIP label
    /// property, no Info dictionary.
    /// </summary>
    internal static MemoryStream WithMetadataLabel(Guid labelId)
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog /Metadata 2 0 R >>");
        var xmp = BuildXmpBody(labelId);
        var metadataOffset = writer.WriteStreamObject(
            2,
            $"<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>",
            xmp);
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = metadataOffset },
            "/Root 1 0 R");
        return writer.ToStream();
    }

    /// <summary>The same active MSIP label property appears in both Info and Metadata XMP.</summary>
    internal static MemoryStream WithDuplicateInfoAndMetadataLabel(Guid labelId)
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog /Metadata 3 0 R >>");
        var infoOffset = writer.WriteObject(2, $"<< /MSIP_Label_{labelId:D}_Enabled (true) >>");
        var xmp = BuildXmpBody(labelId);
        var metadataOffset = writer.WriteStreamObject(
            3,
            $"<< /Type /Metadata /Subtype /XML /Length {xmp.Length} >>",
            xmp);
        writer.WriteClassicXref(
            4,
            new Dictionary<int, long> { [1] = rootOffset, [2] = infoOffset, [3] = metadataOffset },
            "/Root 1 0 R /Info 2 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// The trailer declares /Encrypt (an indirect reference the reader never resolves) while the
    /// Info dictionary still carries a plainly readable, active MSIP label property.
    /// </summary>
    internal static MemoryStream WithEncryptedInfoLabel(Guid labelId)
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var infoOffset = writer.WriteObject(2, $"<< /MSIP_Label_{labelId:D}_Enabled (true) >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = infoOffset },
            "/Root 1 0 R /Info 2 0 R /Encrypt 99 0 R");
        return writer.ToStream();
    }

    /// <summary>The trailer declares /Encrypt; there is no Info dictionary and no Metadata.</summary>
    internal static MemoryStream WithEncryptedNoLabel()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        writer.WriteClassicXref(
            2,
            new Dictionary<int, long> { [1] = rootOffset },
            "/Root 1 0 R /Encrypt 99 0 R");
        return writer.ToStream();
    }

    /// <summary>The trailer declares /Encrypt, whose dictionary records /Filter and /SubFilter.</summary>
    internal static MemoryStream WithEncryptionFilterAndSubFilter()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var encryptOffset = writer.WriteObject(
            2,
            "<< /Filter /Standard /SubFilter /adbe.pkcs7.s5 >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = encryptOffset },
            "/Root 1 0 R /Encrypt 2 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// A Metadata stream whose /Filter is neither absent nor /FlateDecode. When
    /// <paramref name="encrypted"/> is <see langword="true"/> the trailer also declares /Encrypt,
    /// so the unreadable metadata must be tolerated (contributing no GUID) rather than reported as
    /// unsupported; when <see langword="false"/> the same unreadable filter must surface as
    /// unsupported.
    /// </summary>
    internal static MemoryStream WithUnsupportedMetadataFilter(bool encrypted)
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog /Metadata 2 0 R >>");
        var metadataOffset = writer.WriteStreamObject(
            2,
            "<< /Type /Metadata /Subtype /XML /Filter /DCTDecode /Length 4 >>",
            "junk"u8.ToArray());
        var trailerExtra = encrypted ? "/Root 1 0 R /Encrypt 99 0 R" : "/Root 1 0 R";
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = metadataOffset },
            trailerExtra);
        return writer.ToStream();
    }

    /// <summary>
    /// The trailer declares /Encrypt; the /Metadata stream declares /FlateDecode but its raw bytes
    /// are not valid zlib data - the realistic shape of ciphertext flowing through an otherwise
    /// ordinary filter, which must be tolerated (contributing no GUID) rather than reported as
    /// invalid or unsupported.
    /// </summary>
    internal static MemoryStream WithEncryptedCiphertextMetadata()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog /Metadata 2 0 R >>");
        byte[] ciphertext = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        var metadataOffset = writer.WriteStreamObject(
            2,
            $"<< /Type /Metadata /Subtype /XML /Filter /FlateDecode /Length {ciphertext.Length} >>",
            ciphertext);
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = metadataOffset },
            "/Root 1 0 R /Encrypt 99 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// The trailer declares /Encrypt; the catalog's /Metadata reference points at an object number
    /// that has no cross-reference entry at all. This is structural file corruption, independent
    /// of encryption, and must always surface as invalid - never silently tolerated the way an
    /// undecodable-but-resolvable metadata stream is.
    /// </summary>
    internal static MemoryStream WithEncryptedDanglingMetadataReference()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog /Metadata 77 0 R >>");
        writer.WriteClassicXref(
            2,
            new Dictionary<int, long> { [1] = rootOffset },
            "/Root 1 0 R /Encrypt 99 0 R");
        return writer.ToStream();
    }

    /// <summary>The Info dictionary's active MSIP property key has a GUID segment that does not parse.</summary>
    internal static MemoryStream WithMalformedInfoLabel()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var infoOffset = writer.WriteObject(2, "<< /MSIP_Label_not-a-guid_Enabled (true) >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = infoOffset },
            "/Root 1 0 R /Info 2 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// The Info dictionary's value nests arrays two levels past <see cref="PdfLexer.MaxNestingDepth"/>,
    /// so parsing the indirect object itself fails before any string property is inspected.
    /// </summary>
    internal static MemoryStream WithExcessiveNesting()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");
        var nestedArray = new string('[', PdfLexer.MaxNestingDepth + 2) +
            new string(']', PdfLexer.MaxNestingDepth + 2);
        var infoOffset = writer.WriteObject(2, $"<< /Nested {nestedArray} >>");
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = infoOffset },
            "/Root 1 0 R /Info 2 0 R");
        return writer.ToStream();
    }

    /// <summary>
    /// The Info dictionary is object 3, reached only through an /ObjStm (object 2) whose declared
    /// <c>/N</c> is one past <see cref="PdfLexer.MaxCollectionItems"/>, so resolving it fails on the
    /// object-stream header itself, before any entry pair is read.
    /// </summary>
    internal static MemoryStream WithExcessiveObjectStreamEntryCount()
    {
        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog >>");

        var (objStmData, first) = BuildObjStmContent(
            [(3, "<< /MSIP_Label_11111111-1111-1111-1111-111111111111_Enabled (true) >>")]);
        var compressed = Compress(objStmData);
        var excessiveCount = PdfLexer.MaxCollectionItems + 1;
        var objStmOffset = writer.WriteStreamObject(
            2,
            $"<< /Type /ObjStm /N {excessiveCount} /First {first} /Filter /FlateDecode /Length {compressed.Length} >>",
            compressed);

        var xrefStreamOffset = writer.Position;
        var entries = new Dictionary<int, (byte Type, long Field2, int Field3)>
        {
            [0] = (0, 0, 65535),
            [1] = (1, rootOffset, 0),
            [2] = (1, objStmOffset, 0),
            [3] = (2, 2, 0),
            [4] = (1, xrefStreamOffset, 0),
        };

        var xrefOffset = writer.WriteXrefStream(
            4,
            5,
            entries,
            "/Root 1 0 R /Info 3 0 R",
            useIndex: false);
        writer.WriteStartXref(xrefOffset);
        return writer.ToStream();
    }

    /// <summary>
    /// The catalog's /Metadata is a real FlateDecode stream whose decompressed XMP is one byte past
    /// the reader's 4 MiB decoded-metadata cap, so it inflates successfully but is still rejected.
    /// </summary>
    internal static MemoryStream WithOversizedDecompressedMetadata()
    {
        const int maxDecodedMetadataBytes = 4 * 1024 * 1024;

        var writer = new PdfWriter();
        writer.WriteHeader();
        var rootOffset = writer.WriteObject(1, "<< /Type /Catalog /Metadata 2 0 R >>");
        var xml = Encoding.ASCII.GetBytes(
            $"<root>{new string(' ', maxDecodedMetadataBytes + 1)}</root>");
        var compressed = Compress(xml);
        var metadataOffset = writer.WriteStreamObject(
            2,
            $"<< /Type /Metadata /Subtype /XML /Filter /FlateDecode /Length {compressed.Length} >>",
            compressed);
        writer.WriteClassicXref(
            3,
            new Dictionary<int, long> { [1] = rootOffset, [2] = metadataOffset },
            "/Root 1 0 R");
        return writer.ToStream();
    }

    private static byte[] BuildXmpBody(Guid labelId) =>
        Encoding.UTF8.GetBytes(
            $"""
            <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:any-msip-uri">
              <rdf:Description msip:MSIP_Label_{labelId:D}_Enabled="true" />
            </rdf:RDF>
            """);

    private static (byte[] Data, int First) BuildObjStmContent(
        IReadOnlyList<(int ObjectNumber, string ValueText)> objects)
    {
        var header = new StringBuilder();
        var body = new StringBuilder();
        var offsets = new List<int>();

        foreach (var (_, valueText) in objects)
        {
            offsets.Add(body.Length);
            body.Append(valueText);
            body.Append(' ');
        }

        for (var i = 0; i < objects.Count; i++)
        {
            header.Append(objects[i].ObjectNumber);
            header.Append(' ');
            header.Append(offsets[i]);
            header.Append(' ');
        }

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        var bodyBytes = Encoding.ASCII.GetBytes(body.ToString());
        var combined = new byte[headerBytes.Length + bodyBytes.Length];
        headerBytes.CopyTo(combined, 0);
        bodyBytes.CopyTo(combined, headerBytes.Length);
        return (combined, headerBytes.Length);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private sealed class PdfWriter
    {
        private readonly MemoryStream _stream = new();

        internal long Position => _stream.Position;

        internal void WriteHeader() => Append("%PDF-1.7\n");

        internal void Append(string ascii)
        {
            var bytes = Encoding.ASCII.GetBytes(ascii);
            _stream.Write(bytes, 0, bytes.Length);
        }

        internal void AppendBytes(byte[] data) => _stream.Write(data, 0, data.Length);

        internal long WriteObject(int number, string body)
        {
            var offset = Position;
            Append($"{number} 0 obj\n{body}\nendobj\n");
            return offset;
        }

        internal long WriteStreamObject(int number, string dictText, byte[] data)
        {
            var offset = Position;
            Append($"{number} 0 obj\n{dictText}\nstream\n");
            AppendBytes(data);
            Append("\nendstream\nendobj\n");
            return offset;
        }

        internal long WriteClassicXref(
            int size,
            IReadOnlyDictionary<int, long> offsets,
            string trailerExtra,
            long? startxrefOverride = null)
        {
            var xrefOffset = Position;
            Append("xref\n");

            var allEntries = new SortedDictionary<int, long?> { [0] = null };
            foreach (var pair in offsets)
            {
                allEntries[pair.Key] = pair.Value;
            }

            var keys = allEntries.Keys.ToList();
            var index = 0;
            while (index < keys.Count)
            {
                var start = keys[index];
                var count = 1;
                while (index + count < keys.Count && keys[index + count] == start + count)
                {
                    count++;
                }

                Append($"{start} {count}\n");
                for (var offsetIndex = 0; offsetIndex < count; offsetIndex++)
                {
                    var offset = allEntries[keys[index + offsetIndex]];
                    Append(offset is null
                        ? "0000000000 65535 f \n"
                        : $"{offset.Value:D10} 00000 n \n");
                }

                index += count;
            }

            Append("trailer\n");
            Append($"<< /Size {size} {trailerExtra} >>\n");
            WriteStartXref(startxrefOverride ?? xrefOffset);
            return xrefOffset;
        }

        internal long WriteXrefStream(
            int xrefObjectNumber,
            int size,
            IReadOnlyDictionary<int, (byte Type, long Field2, int Field3)> entries,
            string extraDictEntries,
            bool useIndex,
            string filterName = "FlateDecode")
        {
            var objectNumbers = entries.Keys.OrderBy(number => number).ToList();
            var rawTable = new List<byte>();
            var indexText = "";

            if (useIndex)
            {
                var runs = new List<(int Start, int Count)>();
                var index = 0;
                while (index < objectNumbers.Count)
                {
                    var start = objectNumbers[index];
                    var count = 1;
                    while (index + count < objectNumbers.Count &&
                        objectNumbers[index + count] == start + count)
                    {
                        count++;
                    }

                    runs.Add((start, count));
                    index += count;
                }

                indexText = "/Index [" +
                    string.Join(" ", runs.Select(run => $"{run.Start} {run.Count}")) +
                    "] ";
                foreach (var number in objectNumbers)
                {
                    AppendEntry(rawTable, entries[number]);
                }
            }
            else
            {
                for (var number = 0; number < size; number++)
                {
                    AppendEntry(rawTable, entries[number]);
                }
            }

            var compressed = Compress(rawTable.ToArray());
            var dictText =
                $"<< /Type /XRef /W [1 8 2] /Size {size} {indexText}{extraDictEntries} " +
                $"/Filter /{filterName} /Length {compressed.Length} >>";
            return WriteStreamObject(xrefObjectNumber, dictText, compressed);
        }

        internal void WriteStartXref(long xrefOffset) => Append($"startxref\n{xrefOffset}\n%%EOF\n");

        internal MemoryStream ToStream()
        {
            _stream.Position = 0;
            return _stream;
        }

        private static void AppendEntry(List<byte> buffer, (byte Type, long Field2, int Field3) entry)
        {
            buffer.Add(entry.Type);
            AppendBigEndian(buffer, entry.Field2, 8);
            AppendBigEndian(buffer, entry.Field3, 2);
        }

        private static void AppendBigEndian(List<byte> buffer, long value, int width)
        {
            for (var index = width - 1; index >= 0; index--)
            {
                buffer.Add((byte)((value >> (index * 8)) & 0xFF));
            }
        }
    }
}
