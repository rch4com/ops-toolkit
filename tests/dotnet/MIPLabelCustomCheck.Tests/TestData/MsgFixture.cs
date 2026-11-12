using System.Buffers.Binary;
using System.Text;

namespace MIPLabelCustomCheck.Tests.TestData;

internal static class MsgFixture
{
    private const uint FreeSector = 0xFFFFFFFF;
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FatSector = 0xFFFFFFFD;
    private const int SectorSize = 512;

    private static ReadOnlySpan<byte> Signature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    internal static byte[] CreateWithPublicStringProperties(IReadOnlyDictionary<string, string> properties)
    {
        var streams = BuildNamedPropertyStreams(properties);
        return CreateCompoundFile(streams, ["__nameid_version1.0"]);
    }

    private static Dictionary<string, byte[]> BuildNamedPropertyStreams(
        IReadOnlyDictionary<string, string> properties)
    {
        var entryStream = new MemoryStream();
        var stringStream = new MemoryStream();
        var streams = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["__nameid_version1.0/__substg1.0_00030102"] = [],
            ["__nameid_version1.0/__substg1.0_00040102"] = []
        };

        var propertyIndex = 0;
        foreach (var (name, value) in properties)
        {
            var nameOffset = checked((uint)stringStream.Length);
            WriteUInt32(stringStream, checked((uint)Encoding.Unicode.GetByteCount(name)));
            var encodedName = Encoding.Unicode.GetBytes(name);
            stringStream.Write(encodedName);
            while (stringStream.Length % 4 != 0)
            {
                stringStream.WriteByte(0);
            }

            WriteUInt32(entryStream, nameOffset);
            WriteUInt32(entryStream, checked(((uint)propertyIndex << 16) | (2u << 1) | 1u));

            var propertyId = checked(0x8000 + propertyIndex);
            streams[$"__substg1.0_{propertyId:X4}001F"] = Encoding.Unicode.GetBytes(value + '\0');
            propertyIndex++;
        }

        streams["__nameid_version1.0/__substg1.0_00030102"] = entryStream.ToArray();
        streams["__nameid_version1.0/__substg1.0_00040102"] = stringStream.ToArray();
        return streams;
    }

    private static byte[] CreateCompoundFile(
        Dictionary<string, byte[]> streams,
        IReadOnlyCollection<string> storages)
    {
        var directory = BuildDirectory(streams, storages);
        var directorySectorCount = checked((directory.Count * 128 + SectorSize - 1) / SectorSize);
        var streamSectorCounts = streams.Values
            .Select(value => Math.Max(8, checked((value.Length + SectorSize - 1) / SectorSize)))
            .ToArray();
        var dataSectorCount = streamSectorCounts.Sum();
        var fatSectorCount = 1;
        while (checked(fatSectorCount * (SectorSize / sizeof(uint))) <
               checked(fatSectorCount + directorySectorCount + dataSectorCount))
        {
            fatSectorCount++;
        }

        var firstDirectorySector = checked((uint)fatSectorCount);
        var firstDataSector = checked(firstDirectorySector + (uint)directorySectorCount);
        var physicalSectorCount = checked(fatSectorCount + directorySectorCount + dataSectorCount);
        var bytes = new byte[checked((physicalSectorCount + 1) * SectorSize)];

        WriteHeader(bytes, fatSectorCount, firstDirectorySector);
        for (var sectorId = 0; sectorId < physicalSectorCount; sectorId++)
        {
            WriteFatEntry(bytes, checked((uint)sectorId), FreeSector);
        }

        for (var index = 0; index < fatSectorCount; index++)
        {
            bytes.AsSpan(checked((index + 1) * SectorSize), SectorSize).Fill(0xFF);
            WriteFatEntry(bytes, checked((uint)index), FatSector);
        }

        for (var index = 0; index < directorySectorCount; index++)
        {
            var sector = checked(firstDirectorySector + (uint)index);
            WriteFatEntry(bytes, sector, index + 1 == directorySectorCount ? EndOfChain : sector + 1);
        }

        var currentSector = firstDataSector;
        var streamIndex = 0;
        foreach (var stream in streams)
        {
            var sectorCount = streamSectorCounts[streamIndex++];
            directory[stream.Key].StartSector = currentSector;
            directory[stream.Key].Size = checked((ulong)(sectorCount * SectorSize));
            stream.Value.CopyTo(bytes.AsSpan(SectorOffset(currentSector)));
            for (var index = 0; index < sectorCount; index++)
            {
                var sector = checked(currentSector + (uint)index);
                WriteFatEntry(bytes, sector, index + 1 == sectorCount ? EndOfChain : sector + 1);
            }

            currentSector = checked(currentSector + (uint)sectorCount);
        }

        var directoryOffset = SectorOffset(firstDirectorySector);
        var orderedEntries = directory.Values.OrderBy(entry => entry.Id).ToArray();
        foreach (var entry in orderedEntries)
        {
            WriteDirectoryEntry(bytes, directoryOffset + (entry.Id * 128), entry);
        }

        return bytes;
    }

    private static Dictionary<string, DirectoryEntry> BuildDirectory(
        Dictionary<string, byte[]> streams,
        IReadOnlyCollection<string> storages)
    {
        var entries = new Dictionary<string, DirectoryEntry>(StringComparer.Ordinal);
        var root = new DirectoryEntry(0, "Root Entry", 5) { Child = 1 };
        entries[string.Empty] = root;

        var nextId = 1;
        foreach (var storage in storages)
        {
            entries[storage] = new DirectoryEntry(nextId++, storage, 1);
        }

        foreach (var stream in streams)
        {
            entries[stream.Key] = new DirectoryEntry(nextId++, Path.GetFileName(stream.Key), 2);
        }

        LinkRootChildren(entries);
        foreach (var storage in storages)
        {
            LinkStorageChildren(entries, storage);
        }

        return entries;
    }

    private static void LinkRootChildren(Dictionary<string, DirectoryEntry> entries)
    {
        var rootChildren = entries.Values
            .Where(entry => entry.Id != 0 && !entry.FullPath.Contains('/'))
            .OrderBy(entry => entry.Id)
            .ToArray();
        for (var index = 0; index < rootChildren.Length - 1; index++)
        {
            rootChildren[index].Right = checked((uint)rootChildren[index + 1].Id);
        }
    }

    private static void LinkStorageChildren(Dictionary<string, DirectoryEntry> entries, string storage)
    {
        var children = entries.Values
            .Where(entry => entry.FullPath.StartsWith(storage + "/", StringComparison.Ordinal))
            .OrderBy(entry => entry.Id)
            .ToArray();
        if (children.Length == 0)
        {
            return;
        }

        entries[storage].Child = checked((uint)children[0].Id);
        for (var index = 0; index < children.Length - 1; index++)
        {
            children[index].Right = checked((uint)children[index + 1].Id);
        }
    }

    private static void WriteHeader(byte[] bytes, int fatSectorCount, uint firstDirectorySector)
    {
        Signature.CopyTo(bytes);
        WriteUInt16(bytes, 24, 0x003E);
        WriteUInt16(bytes, 26, 3);
        WriteUInt16(bytes, 28, 0xFFFE);
        WriteUInt16(bytes, 30, 9);
        WriteUInt16(bytes, 32, 6);
        WriteUInt32(bytes, 40, 0);
        WriteUInt32(bytes, 44, checked((uint)fatSectorCount));
        WriteUInt32(bytes, 48, firstDirectorySector);
        WriteUInt32(bytes, 56, 4096);
        WriteUInt32(bytes, 60, EndOfChain);
        WriteUInt32(bytes, 64, 0);
        WriteUInt32(bytes, 68, EndOfChain);
        WriteUInt32(bytes, 72, 0);
        for (var index = 0; index < 109; index++)
        {
            WriteUInt32(bytes, 76 + (index * sizeof(uint)), FreeSector);
        }

        for (var index = 0; index < fatSectorCount; index++)
        {
            WriteUInt32(bytes, 76 + (index * sizeof(uint)), checked((uint)index));
        }
    }

    private static void WriteDirectoryEntry(byte[] bytes, int offset, DirectoryEntry entry)
    {
        var encodedName = Encoding.Unicode.GetBytes(entry.Name + '\0');
        encodedName.CopyTo(bytes.AsSpan(offset));
        WriteUInt16(bytes, offset + 64, checked((ushort)encodedName.Length));
        bytes[offset + 66] = entry.Type;
        bytes[offset + 67] = 1;
        WriteUInt32(bytes, offset + 68, FreeSector);
        WriteUInt32(bytes, offset + 72, entry.Right);
        WriteUInt32(bytes, offset + 76, entry.Child);
        WriteUInt32(bytes, offset + 116, entry.StartSector);
        WriteUInt64(bytes, offset + 120, entry.Size);
    }

    private static int SectorOffset(uint sectorId) => checked(((int)sectorId + 1) * SectorSize);

    private static void WriteFatEntry(byte[] bytes, uint sectorId, uint value)
    {
        var entryIndex = checked((int)sectorId);
        WriteUInt32(bytes, SectorSize + (entryIndex * sizeof(uint)), value);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, sizeof(ulong)), value);

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private sealed class DirectoryEntry(int id, string fullPath, byte type)
    {
        internal int Id { get; } = id;
        internal string FullPath { get; } = fullPath;
        internal string Name { get; } = fullPath[(fullPath.LastIndexOf('/') + 1)..];
        internal byte Type { get; } = type;
        internal uint Right { get; set; } = FreeSector;
        internal uint Child { get; set; } = FreeSector;
        internal uint StartSector { get; set; } = EndOfChain;
        internal ulong Size { get; set; }
    }
}
