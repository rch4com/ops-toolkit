using System.Buffers.Binary;
using System.Text;
using MIPLabelCustomCheck.Internal.CompoundFile;

namespace MIPLabelCustomCheck.Internal;

internal static class MsgLabelReader
{
    private const string NameIdStorage = "__nameid_version1.0";
    private const string EntryStreamPath = "__nameid_version1.0/__substg1.0_00030102";
    private const string StringStreamPath = "__nameid_version1.0/__substg1.0_00040102";
    private const string EntryStreamRootPath = "__substg1.0_00030102";
    private const string StringStreamRootPath = "__substg1.0_00040102";
    private const string LabelPrefix = "MSIP_Label_";
    private const int EntrySize = 8;
    private const int PsPublicStringsGuidIndex = 2;
    private const int StringNamedPropertyKind = 1;

    internal static LabelReadResult Read(Stream stream, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new CompoundFileReader(stream, leaveOpen: true);
            using var view = new CompoundFileReaderView(reader);
            return Read(view, includeDetails);
        }
        catch (InvalidDataException)
        {
            return LabelReadResult.Invalid();
        }
    }

    internal static LabelReadResult Read(ICompoundFileView view, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!view.Contains(NameIdStorage) ||
            !TryReadNameIdStreams(view, out var entryBytes, out var stringBytes))
        {
            return LabelReadResult.Unlabeled();
        }

        var properties = new List<KeyValuePair<string, string>>();
        foreach (var mapping in ReadMappings(entryBytes, stringBytes))
        {
            if (!mapping.Name.StartsWith(LabelPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (view.TryReadStream(BuildStringPropertyStreamName(mapping.PropertyId), out var valueBytes))
            {
                properties.Add(new KeyValuePair<string, string>(mapping.Name, DecodeUnicodeValue(valueBytes)));
            }
        }

        return EmlLabelReader.FromProperties(properties, includeDetails, LabelSource.MsgNamedProperty);
    }

    private static bool TryReadNameIdStreams(
        ICompoundFileView view,
        out byte[] entryBytes,
        out byte[] stringBytes)
    {
        entryBytes = [];
        stringBytes = [];
        if (view.TryReadStream(EntryStreamPath, out entryBytes) &&
            view.TryReadStream(StringStreamPath, out stringBytes))
        {
            return true;
        }

        return view.TryReadStream(EntryStreamRootPath, out entryBytes) &&
            view.TryReadStream(StringStreamRootPath, out stringBytes);
    }

    private static IReadOnlyList<NamedPropertyMapping> ReadMappings(
        ReadOnlySpan<byte> entryBytes,
        ReadOnlySpan<byte> stringBytes)
    {
        var mappings = new List<NamedPropertyMapping>();
        // [MS-OXMSG] specifies that MSG named-property mappings live only under
        // __nameid_version1.0. Its entry stream maps property indices to names; GUID index 2 is
        // PS_PUBLIC_STRINGS, and string named properties use kind 1 with the name in the string
        // stream. The MIP SDK metadata shape supplies the MSIP_Label_{guid}_Enabled keys.
        for (var offset = 0; offset + EntrySize <= entryBytes.Length; offset += EntrySize)
        {
            var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(entryBytes[offset..]);
            var descriptor = BinaryPrimitives.ReadUInt32LittleEndian(entryBytes[(offset + 4)..]);
            if (descriptor == 0)
            {
                continue;
            }

            var kind = (int)(descriptor & 0x1);
            var guidIndex = (int)((descriptor & 0xFFFF) >> 1);
            if (kind != StringNamedPropertyKind || guidIndex != PsPublicStringsGuidIndex)
            {
                continue;
            }

            var propertyIndex = checked((int)(descriptor >> 16));
            var name = DecodeNameString(stringBytes, checked((int)nameOffset));
            mappings.Add(new NamedPropertyMapping(checked(0x8000 + propertyIndex), name));
        }

        return mappings;
    }

    private static string DecodeNameString(ReadOnlySpan<byte> stringBytes, int offset)
    {
        if (offset < 0 || offset + sizeof(uint) > stringBytes.Length)
        {
            throw new InvalidDataException("The MSG named-property string offset is invalid.");
        }

        var byteLength = InputGuard.CheckedLength(
            BinaryPrimitives.ReadUInt32LittleEndian(stringBytes[offset..]),
            stringBytes.Length - offset - sizeof(uint),
            "The MSG named-property string");
        if (byteLength % 2 != 0)
        {
            throw new InvalidDataException("The MSG named-property string length is invalid.");
        }

        return Encoding.Unicode.GetString(stringBytes.Slice(offset + sizeof(uint), byteLength));
    }

    private static string DecodeUnicodeValue(byte[] valueBytes)
    {
        if (valueBytes.Length % 2 != 0)
        {
            throw new InvalidDataException("The MSG named-property value length is invalid.");
        }

        var value = Encoding.Unicode.GetString(valueBytes);
        var terminator = value.IndexOf('\0');
        return terminator >= 0 ? value[..terminator] : value;
    }

    private static string BuildStringPropertyStreamName(int propertyId) =>
        $"__substg1.0_{propertyId:X4}001F";

    private readonly record struct NamedPropertyMapping(int PropertyId, string Name);

    private sealed class CompoundFileReaderView(CompoundFileReader reader) : ICompoundFileView
    {
        public bool Contains(string path) => reader.Contains(path);

        public bool TryReadStream(string path, out byte[] bytes) => reader.TryReadStream(path, out bytes);

        public void Dispose() => reader.Dispose();
    }
}
