using System.Buffers.Binary;
using System.Text;

namespace MIPLabelCustomCheck.Tests.TestData;

internal sealed class CompoundFileFixture
{
    internal const uint FreeSector = 0xFFFFFFFF;
    internal const uint EndOfChain = 0xFFFFFFFE;
    internal const uint FatSector = 0xFFFFFFFD;
    internal const uint DifatSector = 0xFFFFFFFC;
    internal const int MiniEntryId = 2;
    internal const int RegularEntryId = 3;

    private static ReadOnlySpan<byte> Signature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private readonly bool _useDifat;

    private CompoundFileFixture(ushort majorVersion, bool useDifat = false)
    {
        _useDifat = useDifat;
        MajorVersion = majorVersion;
        SectorSize = majorVersion == 3 ? 512 : 4096;
        MiniPayload = [0x00, 0x11, 0x22, 0xFE, 0xFF];
        RegularPayload = Enumerable.Range(0, (48 * 1024) + 123)
            .Select(index => (byte)(index * 31))
            .ToArray();

        FatSectorId = 0;
        FatSectorCount = useDifat ? 110 : 1;
        DifatSectorId = useDifat ? checked((uint)FatSectorCount) : EndOfChain;
        FirstDirectorySectorId = checked((uint)FatSectorCount + (useDifat ? 1u : 0u));
        DirectorySectorCount = (5 * 128 + SectorSize - 1) / SectorSize;
        MiniFatSectorId = checked(FirstDirectorySectorId + (uint)DirectorySectorCount);
        RootMiniStreamSectorId = MiniFatSectorId + 1;
        RegularFirstSectorId = RootMiniStreamSectorId + 1;
        RegularSectorCount = (RegularPayload.Length + SectorSize - 1) / SectorSize;
        PhysicalSectorCount = checked((int)RegularFirstSectorId + RegularSectorCount);
        Bytes = CreateBytes();
    }

    internal ushort MajorVersion { get; }

    internal int SectorSize { get; }

    internal byte[] MiniPayload { get; }

    internal byte[] RegularPayload { get; }

    internal uint FatSectorId { get; }

    internal int FatSectorCount { get; }

    internal uint DifatSectorId { get; }

    internal uint FirstDirectorySectorId { get; }

    internal int DirectorySectorCount { get; }

    internal uint LastDirectorySectorId =>
        checked(FirstDirectorySectorId + (uint)DirectorySectorCount - 1);

    internal uint MiniFatSectorId { get; }

    internal uint RootMiniStreamSectorId { get; }

    internal uint RegularFirstSectorId { get; }

    internal int RegularSectorCount { get; }

    internal int PhysicalSectorCount { get; }

    internal byte[] Bytes { get; }

    internal static CompoundFileFixture CreateV3() => new(3);

    internal static CompoundFileFixture CreateV3WithDifat() => new(3, useDifat: true);

    internal static CompoundFileFixture CreateV4() => new(4);

    internal static byte[] CreateV3StorageChain(int storageCount)
    {
        var entries = CreateDirectoryEntries(storageCount + 1);
        entries[0] = DirectorySpec.Root(storageCount == 0 ? FreeSector : 1);
        for (var index = 1; index <= storageCount; index++)
        {
            entries[index] = DirectorySpec.Storage(
                $"S{index:D3}",
                index == storageCount ? FreeSector : checked((uint)index + 1));
        }

        return CreateV3DirectoryFile(entries);
    }

    internal static byte[] CreateV3SiblingChain(int streamCount)
    {
        var entries = CreateDirectoryEntries(streamCount + 1);
        entries[0] = DirectorySpec.Root(streamCount == 0 ? FreeSector : 1);
        for (var index = 1; index <= streamCount; index++)
        {
            entries[index] = DirectorySpec.Stream(
                $"N{index:D5}",
                rightSibling: index == streamCount ? FreeSector : checked((uint)index + 1));
        }

        return CreateV3DirectoryFile(entries);
    }

    internal static byte[] CreateV3WideNestedTree(int streamCount)
    {
        var entries = CreateDirectoryEntries(streamCount + 2);
        entries[0] = DirectorySpec.Root(1);
        entries[1] = DirectorySpec.Storage(new string('P', 31), streamCount == 0 ? FreeSector : 2);
        for (var index = 0; index < streamCount; index++)
        {
            entries[index + 2] = DirectorySpec.Stream(
                $"N{index:D5}".PadRight(31, 'X'),
                rightSibling: FreeSector);
        }

        entries[1].Child = LinkBalancedSiblingTree(entries, 2, entries.Length - 1);
        return CreateV3DirectoryFile(entries);
    }

    internal byte[] CloneBytes() => (byte[])Bytes.Clone();

    internal int SectorOffset(uint sectorId) => checked(((int)sectorId + 1) * SectorSize);

    internal int DirectoryEntryOffset(int entryId) =>
        checked(SectorOffset(FirstDirectorySectorId) + (entryId * 128));

    internal void SetFatEntry(byte[] bytes, uint sectorId, uint value)
    {
        var entriesPerFatSector = SectorSize / 4;
        var tableSectorIndex = checked((int)(sectorId / (uint)entriesPerFatSector));
        var entryIndex = checked((int)(sectorId % (uint)entriesPerFatSector));
        WriteUInt32(
            bytes,
            SectorOffset(checked((uint)tableSectorIndex)) + (entryIndex * 4),
            value);
    }

    internal void SetMiniFatEntry(byte[] bytes, uint miniSectorId, uint value) =>
        WriteUInt32(bytes, SectorOffset(MiniFatSectorId) + checked((int)miniSectorId * 4), value);

    internal void SetDirectoryNameLength(byte[] bytes, int entryId, ushort value) =>
        WriteUInt16(bytes, DirectoryEntryOffset(entryId) + 64, value);

    internal void SetDirectoryType(byte[] bytes, int entryId, byte value) =>
        bytes[DirectoryEntryOffset(entryId) + 66] = value;

    internal void SetDirectoryRightSibling(byte[] bytes, int entryId, uint value) =>
        WriteUInt32(bytes, DirectoryEntryOffset(entryId) + 72, value);

    internal void SetDirectoryChild(byte[] bytes, int entryId, uint value) =>
        WriteUInt32(bytes, DirectoryEntryOffset(entryId) + 76, value);

    internal void SetDirectorySize(byte[] bytes, int entryId, ulong value) =>
        WriteUInt64(bytes, DirectoryEntryOffset(entryId) + 120, value);

    internal void SetHeaderUInt16(byte[] bytes, int offset, ushort value) =>
        WriteUInt16(bytes, offset, value);

    internal void SetHeaderUInt32(byte[] bytes, int offset, uint value) =>
        WriteUInt32(bytes, offset, value);

    private byte[] CreateBytes()
    {
        var bytes = new byte[checked((PhysicalSectorCount + 1) * SectorSize)];
        WriteHeader(bytes);
        WriteFat(bytes);
        WriteDifat(bytes);
        WriteMiniFat(bytes);
        WriteDirectory(bytes);
        MiniPayload.CopyTo(bytes.AsSpan(SectorOffset(RootMiniStreamSectorId)));
        RegularPayload.CopyTo(bytes.AsSpan(SectorOffset(RegularFirstSectorId)));
        return bytes;
    }

    private void WriteHeader(byte[] bytes)
    {
        Signature.CopyTo(bytes);
        WriteUInt16(bytes, 24, 0x003E);
        WriteUInt16(bytes, 26, MajorVersion);
        WriteUInt16(bytes, 28, 0xFFFE);
        WriteUInt16(bytes, 30, MajorVersion == 3 ? (ushort)9 : (ushort)12);
        WriteUInt16(bytes, 32, 6);
        WriteUInt32(bytes, 40, MajorVersion == 3 ? 0u : checked((uint)DirectorySectorCount));
        WriteUInt32(bytes, 44, checked((uint)FatSectorCount));
        WriteUInt32(bytes, 48, FirstDirectorySectorId);
        WriteUInt32(bytes, 56, 4096);
        WriteUInt32(bytes, 60, MiniFatSectorId);
        WriteUInt32(bytes, 64, 1);
        WriteUInt32(bytes, 68, _useDifat ? DifatSectorId : EndOfChain);
        WriteUInt32(bytes, 72, _useDifat ? 1u : 0u);

        for (var index = 0; index < 109; index++)
        {
            WriteUInt32(bytes, 76 + (index * 4), FreeSector);
        }

        for (var index = 0; index < Math.Min(109, FatSectorCount); index++)
        {
            WriteUInt32(bytes, 76 + (index * 4), checked((uint)index));
        }
    }

    private void WriteFat(byte[] bytes)
    {
        for (var index = 0; index < FatSectorCount; index++)
        {
            bytes.AsSpan(SectorOffset(checked((uint)index)), SectorSize).Fill(0xFF);
            SetFatEntry(bytes, checked((uint)index), FatSector);
        }

        if (_useDifat)
        {
            SetFatEntry(bytes, DifatSectorId, DifatSector);
        }

        for (var index = 0; index < DirectorySectorCount; index++)
        {
            var sectorId = checked(FirstDirectorySectorId + (uint)index);
            var next = index + 1 == DirectorySectorCount ? EndOfChain : sectorId + 1;
            SetFatEntry(bytes, sectorId, next);
        }

        SetFatEntry(bytes, MiniFatSectorId, EndOfChain);
        SetFatEntry(bytes, RootMiniStreamSectorId, EndOfChain);

        for (var index = 0; index < RegularSectorCount; index++)
        {
            var sectorId = checked(RegularFirstSectorId + (uint)index);
            var next = index + 1 == RegularSectorCount ? EndOfChain : sectorId + 1;
            SetFatEntry(bytes, sectorId, next);
        }
    }

    private void WriteDifat(byte[] bytes)
    {
        if (!_useDifat)
        {
            return;
        }

        var offset = SectorOffset(DifatSectorId);
        bytes.AsSpan(offset, SectorSize).Fill(0xFF);
        for (var index = 109; index < FatSectorCount; index++)
        {
            WriteUInt32(bytes, offset + ((index - 109) * 4), checked((uint)index));
        }

        WriteUInt32(bytes, offset + SectorSize - 4, EndOfChain);
    }

    private void WriteMiniFat(byte[] bytes)
    {
        var offset = SectorOffset(MiniFatSectorId);
        bytes.AsSpan(offset, SectorSize).Fill(0xFF);
        SetMiniFatEntry(bytes, 0, EndOfChain);
    }

    private void WriteDirectory(byte[] bytes)
    {
        WriteDirectoryEntry(
            bytes,
            0,
            "Root Entry",
            type: 5,
            rightSibling: FreeSector,
            child: 1,
            startSector: RootMiniStreamSectorId,
            size: checked((ulong)SectorSize));
        WriteDirectoryEntry(
            bytes,
            1,
            "Nested",
            type: 1,
            rightSibling: RegularEntryId,
            child: MiniEntryId,
            startSector: EndOfChain,
            size: 0);
        WriteDirectoryEntry(
            bytes,
            MiniEntryId,
            "\u0006DataSpaces",
            type: 2,
            rightSibling: 4,
            child: FreeSector,
            startSector: 0,
            size: checked((ulong)MiniPayload.Length));
        WriteDirectoryEntry(
            bytes,
            RegularEntryId,
            "Regular",
            type: 2,
            rightSibling: FreeSector,
            child: FreeSector,
            startSector: RegularFirstSectorId,
            size: checked((ulong)RegularPayload.Length));
        WriteDirectoryEntry(
            bytes,
            4,
            "Zero",
            type: 2,
            rightSibling: FreeSector,
            child: FreeSector,
            startSector: EndOfChain,
            size: 0);
    }

    private void WriteDirectoryEntry(
        byte[] bytes,
        int entryId,
        string name,
        byte type,
        uint rightSibling,
        uint child,
        uint startSector,
        ulong size)
    {
        var offset = DirectoryEntryOffset(entryId);
        var encodedName = Encoding.Unicode.GetBytes(name + '\0');
        encodedName.CopyTo(bytes.AsSpan(offset));
        WriteUInt16(bytes, offset + 64, checked((ushort)encodedName.Length));
        bytes[offset + 66] = type;
        bytes[offset + 67] = 1;
        WriteUInt32(bytes, offset + 68, FreeSector);
        WriteUInt32(bytes, offset + 72, rightSibling);
        WriteUInt32(bytes, offset + 76, child);
        WriteUInt32(bytes, offset + 116, startSector);
        WriteUInt64(bytes, offset + 120, size);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8), value);

    private static DirectorySpec[] CreateDirectoryEntries(int count)
    {
        var entries = new DirectorySpec[count];
        for (var index = 0; index < entries.Length; index++)
        {
            entries[index] = DirectorySpec.Empty();
        }

        return entries;
    }

    private static byte[] CreateV3DirectoryFile(DirectorySpec[] entries)
    {
        const int sectorSize = 512;
        const int entriesPerFatSector = sectorSize / sizeof(uint);
        var directorySectorCount = checked((entries.Length * 128 + sectorSize - 1) / sectorSize);
        var fatSectorCount = 1;
        while (checked(fatSectorCount * entriesPerFatSector) <
               checked(fatSectorCount + directorySectorCount))
        {
            fatSectorCount++;
        }

        if (fatSectorCount > 109)
        {
            throw new InvalidOperationException("The test directory fixture requires a DIFAT sector.");
        }

        var firstDirectorySector = checked((uint)fatSectorCount);
        var physicalSectorCount = checked(fatSectorCount + directorySectorCount);
        var bytes = new byte[checked((physicalSectorCount + 1) * sectorSize)];

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
            WriteUInt32(bytes, 76 + (index * 4), FreeSector);
        }

        for (var index = 0; index < fatSectorCount; index++)
        {
            WriteUInt32(bytes, 76 + (index * 4), checked((uint)index));
            bytes.AsSpan((index + 1) * sectorSize, sectorSize).Fill(0xFF);
            WriteV3FatEntry(bytes, checked((uint)index), FatSector, sectorSize);
        }

        for (var index = 0; index < directorySectorCount; index++)
        {
            var sectorId = checked(firstDirectorySector + (uint)index);
            var next = index + 1 == directorySectorCount ? EndOfChain : sectorId + 1;
            WriteV3FatEntry(bytes, sectorId, next, sectorSize);
        }

        var directoryOffset = checked(((int)firstDirectorySector + 1) * sectorSize);
        for (var index = 0; index < entries.Length; index++)
        {
            WriteDirectorySpec(bytes, directoryOffset + (index * 128), entries[index]);
        }

        return bytes;
    }

    private static void WriteV3FatEntry(byte[] bytes, uint sectorId, uint value, int sectorSize)
    {
        var entriesPerFatSector = sectorSize / sizeof(uint);
        var fatSectorIndex = checked((int)(sectorId / (uint)entriesPerFatSector));
        var entryIndex = checked((int)(sectorId % (uint)entriesPerFatSector));
        WriteUInt32(
            bytes,
            checked(((fatSectorIndex + 1) * sectorSize) + (entryIndex * sizeof(uint))),
            value);
    }

    private static void WriteDirectorySpec(byte[] bytes, int offset, DirectorySpec entry)
    {
        if (entry.Type == 0)
        {
            return;
        }

        var encodedName = Encoding.Unicode.GetBytes(entry.Name + '\0');
        encodedName.CopyTo(bytes.AsSpan(offset));
        WriteUInt16(bytes, offset + 64, checked((ushort)encodedName.Length));
        bytes[offset + 66] = entry.Type;
        bytes[offset + 67] = 1;
        WriteUInt32(bytes, offset + 68, entry.Left);
        WriteUInt32(bytes, offset + 72, entry.Right);
        WriteUInt32(bytes, offset + 76, entry.Child);
        WriteUInt32(bytes, offset + 116, EndOfChain);
        WriteUInt64(bytes, offset + 120, 0);
    }

    private static uint LinkBalancedSiblingTree(DirectorySpec[] entries, int first, int last)
    {
        if (first > last)
        {
            return FreeSector;
        }

        var middle = first + ((last - first) / 2);
        entries[middle].Left = LinkBalancedSiblingTree(entries, first, middle - 1);
        entries[middle].Right = LinkBalancedSiblingTree(entries, middle + 1, last);
        return checked((uint)middle);
    }

    private sealed class DirectorySpec
    {
        private DirectorySpec(string name, byte type, uint left, uint right, uint child)
        {
            Name = name;
            Type = type;
            Left = left;
            Right = right;
            Child = child;
        }

        internal string Name { get; }

        internal byte Type { get; }

        internal uint Left { get; set; }

        internal uint Right { get; set; }

        internal uint Child { get; set; }

        internal static DirectorySpec Empty() => new(string.Empty, 0, 0, 0, 0);

        internal static DirectorySpec Root(uint child) =>
            new("Root Entry", 5, FreeSector, FreeSector, child);

        internal static DirectorySpec Storage(string name, uint child) =>
            new(name, 1, FreeSector, FreeSector, child);

        internal static DirectorySpec Stream(string name, uint rightSibling) =>
            new(name, 2, FreeSector, rightSibling, FreeSector);
    }
}
