using System.Buffers.Binary;
using System.Text;

namespace MIPLabelCustomCheck.Internal.CompoundFile;

internal sealed class CompoundFileReader : IDisposable
{
    internal const uint FreeSector = 0xFFFFFFFF;
    internal const uint EndOfChain = 0xFFFFFFFE;
    internal const uint FatSector = 0xFFFFFFFD;
    internal const uint DifatSector = 0xFFFFFFFC;
    internal const int MaxChainEntries = 1_000_000;
    internal const int MaxMetadataStreamBytes = 4 * 1024 * 1024;
    internal const int MaxDirectoryBytes = 16 * 1024 * 1024;
    internal const int MaxFatEntries = 1_000_000;
    internal const int MaxStorageDepth = 256;
    internal const int MaxSiblingTreeDepth = 256;
    internal const int MaxPathCharacters = 8192;
    internal const int MaxTotalRetainedPathCharacters = 256 * 1024;

    private const uint NoStream = 0xFFFFFFFF;
    private const int HeaderSize = 512;
    private const int MiniSectorSize = 64;
    private const uint StandardMiniStreamCutoff = 4096;

    private static ReadOnlySpan<byte> Signature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    // API paths preserve CFB names verbatim and intentionally use exact ordinal lookup.
    private readonly Dictionary<string, CompoundDirectoryEntry> _entriesByPath =
        new(StringComparer.Ordinal);

    private ushort _majorVersion;
    private int _sectorSize;
    private int _physicalSectorCount;
    private uint[] _fat = [];
    private uint[] _miniFat = [];
    private List<uint> _rootMiniStreamChain = [];
    private ulong _rootMiniStreamSize;
    private bool _disposed;

    internal CompoundFileReader(Stream stream, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The compound file stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new NotSupportedException("The compound file stream must be seekable.");
        }

        _stream = stream;
        _leaveOpen = leaveOpen;

        try
        {
            Parse();
        }
        catch (OverflowException exception)
        {
            throw Invalid("The compound file contains an overflowing size or offset.", exception);
        }
    }

    internal bool Contains(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);

        return _entriesByPath.ContainsKey(path);
    }

    internal bool TryReadStream(string path, out byte[] bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(path);

        if (!_entriesByPath.TryGetValue(path, out var entry) ||
            entry.Type != CompoundDirectoryEntryType.Stream)
        {
            bytes = [];
            return false;
        }

        // entry.StreamSize is a ulong straight from the raw directory entry, so it can be any value
        // up to ulong.MaxValue. The unchecked narrowing below never loses safety: values that still
        // fit in a long stay a (huge) positive number and are rejected by the ">maximum" arm below;
        // values beyond long.MaxValue wrap to a negative number and are rejected by the "<0" arm.
        // Either way, InputGuard.CheckedLength throws before any allocation sized by this value.
        var streamLength = InputGuard.CheckedLength(
            unchecked((long)entry.StreamSize),
            MaxMetadataStreamBytes,
            "The requested compound file stream");

        bytes = entry.StreamSize < StandardMiniStreamCutoff
            ? ReadMiniStream(entry, streamLength)
            : ReadRegularStream(entry, streamLength);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private void Parse()
    {
        if (_stream.Length < HeaderSize)
        {
            throw Invalid("The compound file header is truncated.");
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        ReadAt(0, header);

        if (!header[..Signature.Length].SequenceEqual(Signature))
        {
            throw Invalid("The compound file signature is invalid.");
        }

        _majorVersion = ReadUInt16(header, 26);
        var sectorShift = ReadUInt16(header, 30);
        if ((_majorVersion == 3 && sectorShift != 9) ||
            (_majorVersion == 4 && sectorShift != 12) ||
            _majorVersion is not (3 or 4))
        {
            throw Invalid("The compound file major version and sector shift are unsupported.");
        }

        if (ReadUInt16(header, 28) != 0xFFFE)
        {
            throw Invalid("The compound file byte order is invalid.");
        }

        if (ReadUInt16(header, 32) != 6)
        {
            throw Invalid("The compound file mini-sector shift is unsupported.");
        }

        if (ReadUInt32(header, 56) != StandardMiniStreamCutoff)
        {
            throw Invalid("The compound file mini-stream cutoff is unsupported.");
        }

        _sectorSize = 1 << sectorShift;
        if (_sectorSize > HeaderSize)
        {
            Span<byte> headerPadding = stackalloc byte[_sectorSize - HeaderSize];
            ReadAt(HeaderSize, headerPadding);
            if (headerPadding.ContainsAnyExcept((byte)0))
            {
                throw Invalid("The compound file version 4 header padding is not zeroed.");
            }
        }

        if (_stream.Length < checked(_sectorSize * 2L) || _stream.Length % _sectorSize != 0)
        {
            throw Invalid("The compound file is truncated or is not sector aligned.");
        }

        var physicalSectorCount = (_stream.Length / _sectorSize) - 1;
        if (physicalSectorCount <= 0 || physicalSectorCount > MaxFatEntries)
        {
            throw Invalid("The compound file has an unsupported physical sector count.");
        }

        _physicalSectorCount = checked((int)physicalSectorCount);

        var directorySectorCount = ReadUInt32(header, 40);
        if (_majorVersion == 3 && directorySectorCount != 0)
        {
            throw Invalid("A version 3 compound file must not declare directory sectors.");
        }

        var fatSectorCount = ReadUInt32(header, 44);
        var firstDirectorySector = ReadUInt32(header, 48);
        var firstMiniFatSector = ReadUInt32(header, 60);
        var miniFatSectorCount = ReadUInt32(header, 64);
        var firstDifatSector = ReadUInt32(header, 68);
        var difatSectorCount = ReadUInt32(header, 72);

        var fatSectorIds = ReadDifat(
            header,
            fatSectorCount,
            firstDifatSector,
            difatSectorCount,
            out var difatSectorIds);
        _fat = ReadFat(fatSectorIds, difatSectorIds);

        var directoryChainLimit = MaxDirectoryBytes / _sectorSize;
        int? expectedDirectorySectors = null;
        if (_majorVersion == 4)
        {
            if (directorySectorCount == 0 || directorySectorCount > directoryChainLimit)
            {
                throw Invalid("The compound file directory sector count is invalid.");
            }

            expectedDirectorySectors = checked((int)directorySectorCount);
        }

        var directoryChain = WalkRegularChain(
            firstDirectorySector,
            expectedDirectorySectors,
            directoryChainLimit,
            "directory");
        if (directoryChain.Count == 0)
        {
            throw Invalid("The compound file directory is empty.");
        }

        var directoryBytes = ReadWholeSectors(directoryChain, MaxDirectoryBytes, "directory");
        var entries = ParseDirectory(directoryBytes);
        var root = BuildPaths(entries);

        _miniFat = ReadMiniFat(firstMiniFatSector, miniFatSectorCount);
        _rootMiniStreamSize = root.StreamSize;
        var expectedRootSectors = GetExpectedSectorCount(
            root.StreamSize,
            _sectorSize,
            _physicalSectorCount,
            "root mini stream");
        _rootMiniStreamChain = WalkRegularChain(
            root.StartSector,
            expectedRootSectors,
            _physicalSectorCount,
            "root mini stream");
    }

    private List<uint> ReadDifat(
        ReadOnlySpan<byte> header,
        uint fatSectorCount,
        uint firstDifatSector,
        uint difatSectorCount,
        out HashSet<uint> difatSectorIds)
    {
        if (fatSectorCount == 0 || fatSectorCount > _physicalSectorCount)
        {
            throw Invalid("The compound file FAT sector count is invalid.");
        }

        if (difatSectorCount > _physicalSectorCount || difatSectorCount > MaxChainEntries)
        {
            throw Invalid("The compound file DIFAT sector count is invalid.");
        }

        var fatIds = new List<uint>(checked((int)fatSectorCount));
        var seenFatIds = new HashSet<uint>();
        for (var index = 0; index < 109; index++)
        {
            AddDifatEntry(ReadUInt32(header, 76 + (index * sizeof(uint))), fatSectorCount, fatIds, seenFatIds);
        }

        difatSectorIds = [];
        if (difatSectorCount == 0)
        {
            if (firstDifatSector != EndOfChain)
            {
                throw Invalid("The compound file declares an unexpected DIFAT chain.");
            }
        }
        else
        {
            var current = firstDifatSector;
            var entriesPerDifatSector = (_sectorSize / sizeof(uint)) - 1;
            var sector = new byte[_sectorSize];

            for (var chainIndex = 0; chainIndex < difatSectorCount; chainIndex++)
            {
                ValidatePhysicalSector(current, "DIFAT");
                if (!difatSectorIds.Add(current))
                {
                    throw Invalid("The compound file DIFAT chain contains a cycle.");
                }

                ReadSector(current, sector);
                for (var entryIndex = 0; entryIndex < entriesPerDifatSector; entryIndex++)
                {
                    AddDifatEntry(
                        ReadUInt32(sector, entryIndex * sizeof(uint)),
                        fatSectorCount,
                        fatIds,
                        seenFatIds);
                }

                var next = ReadUInt32(sector, entriesPerDifatSector * sizeof(uint));
                var isLast = chainIndex + 1 == difatSectorCount;
                if (isLast)
                {
                    if (next != EndOfChain)
                    {
                        throw Invalid("The compound file DIFAT chain is longer than declared.");
                    }
                }
                else
                {
                    ValidatePhysicalSector(next, "DIFAT");
                    current = next;
                }
            }
        }

        if (fatIds.Count != fatSectorCount)
        {
            throw Invalid("The compound file FAT and DIFAT counts do not agree.");
        }

        if (difatSectorIds.Overlaps(seenFatIds))
        {
            throw Invalid("A compound file sector is marked as both FAT and DIFAT.");
        }

        return fatIds;
    }

    private void AddDifatEntry(
        uint sectorId,
        uint declaredFatSectorCount,
        List<uint> fatIds,
        HashSet<uint> seenFatIds)
    {
        if (sectorId == FreeSector)
        {
            return;
        }

        ValidatePhysicalSector(sectorId, "FAT");
        if (fatIds.Count >= declaredFatSectorCount || !seenFatIds.Add(sectorId))
        {
            throw Invalid("The compound file DIFAT contains inconsistent FAT sector IDs.");
        }

        fatIds.Add(sectorId);
    }

    private uint[] ReadFat(List<uint> fatSectorIds, HashSet<uint> difatSectorIds)
    {
        var entriesPerSector = _sectorSize / sizeof(uint);
        var fatEntryCount = checked((long)fatSectorIds.Count * entriesPerSector);
        if (fatEntryCount < _physicalSectorCount || fatEntryCount > MaxFatEntries)
        {
            throw Invalid("The compound file FAT entry count is invalid.");
        }

        var fat = new uint[checked((int)fatEntryCount)];
        var sector = new byte[_sectorSize];
        for (var sectorIndex = 0; sectorIndex < fatSectorIds.Count; sectorIndex++)
        {
            ReadSector(fatSectorIds[sectorIndex], sector);
            for (var entryIndex = 0; entryIndex < entriesPerSector; entryIndex++)
            {
                fat[(sectorIndex * entriesPerSector) + entryIndex] =
                    ReadUInt32(sector, entryIndex * sizeof(uint));
            }
        }

        foreach (var sectorId in fatSectorIds)
        {
            if (fat[sectorId] != FatSector)
            {
                throw Invalid("A declared FAT sector has the wrong FAT marker.");
            }
        }

        foreach (var sectorId in difatSectorIds)
        {
            if (fat[sectorId] != DifatSector)
            {
                throw Invalid("A declared DIFAT sector has the wrong FAT marker.");
            }
        }

        return fat;
    }

    private uint[] ReadMiniFat(uint firstMiniFatSector, uint miniFatSectorCount)
    {
        if (miniFatSectorCount == 0)
        {
            if (firstMiniFatSector != EndOfChain)
            {
                throw Invalid("The compound file declares an unexpected miniFAT chain.");
            }

            return [];
        }

        var entriesPerSector = _sectorSize / sizeof(uint);
        var miniFatEntryCount = checked((long)miniFatSectorCount * entriesPerSector);
        if (miniFatSectorCount > _physicalSectorCount ||
            miniFatSectorCount > MaxChainEntries ||
            miniFatEntryCount > MaxFatEntries)
        {
            throw Invalid("The compound file miniFAT size is invalid.");
        }

        var chain = WalkRegularChain(
            firstMiniFatSector,
            checked((int)miniFatSectorCount),
            _physicalSectorCount,
            "miniFAT");
        var bytes = ReadWholeSectors(
            chain,
            checked(MaxFatEntries * sizeof(uint)),
            "miniFAT");
        var miniFat = new uint[bytes.Length / sizeof(uint)];
        for (var index = 0; index < miniFat.Length; index++)
        {
            miniFat[index] = ReadUInt32(bytes, index * sizeof(uint));
        }

        return miniFat;
    }

    private List<CompoundDirectoryEntry> ParseDirectory(byte[] bytes)
    {
        var entries = new List<CompoundDirectoryEntry>(bytes.Length / 128);
        for (var id = 0; id < bytes.Length / 128; id++)
        {
            var data = bytes.AsSpan(id * 128, 128);
            var typeValue = data[66];
            if (typeValue is not (0 or 1 or 2 or 5))
            {
                throw Invalid("The compound file directory contains an invalid object type.");
            }

            var type = (CompoundDirectoryEntryType)typeValue;
            var nameLength = ReadUInt16(data, 64);
            string name;
            if (type == CompoundDirectoryEntryType.Empty)
            {
                if (nameLength != 0)
                {
                    throw Invalid("An empty compound file directory entry has a name.");
                }

                name = string.Empty;
            }
            else
            {
                if (nameLength < 2 || nameLength > 64 || (nameLength & 1) != 0)
                {
                    throw Invalid("A compound file directory name length is invalid.");
                }

                if (ReadUInt16(data, nameLength - 2) != 0)
                {
                    throw Invalid("A compound file directory name is not NUL terminated.");
                }

                name = Encoding.Unicode.GetString(data[..(nameLength - 2)]);
                if (name.IndexOf('\0') >= 0 || name.IndexOf('/') >= 0)
                {
                    throw Invalid("A compound file directory name contains an invalid character.");
                }
            }

            var leftSibling = ReadUInt32(data, 68);
            var rightSibling = ReadUInt32(data, 72);
            var child = ReadUInt32(data, 76);
            var startSector = ReadUInt32(data, 116);
            var streamSize = BinaryPrimitives.ReadUInt64LittleEndian(data[120..128]);

            if (type == CompoundDirectoryEntryType.Storage && streamSize != 0)
            {
                throw Invalid("A compound file storage directory entry has a stream size.");
            }

            if (_majorVersion == 3 &&
                type is CompoundDirectoryEntryType.Stream or CompoundDirectoryEntryType.Root &&
                (streamSize >> 32) != 0)
            {
                throw Invalid("A version 3 compound file stream has a non-zero high size DWORD.");
            }

            entries.Add(new CompoundDirectoryEntry(
                id,
                name,
                type,
                leftSibling,
                rightSibling,
                child,
                startSector,
                streamSize));
        }

        if (entries.Count == 0 || entries[0].Type != CompoundDirectoryEntryType.Root)
        {
            throw Invalid("Directory entry zero is not the compound file root storage.");
        }

        for (var index = 1; index < entries.Count; index++)
        {
            if (entries[index].Type == CompoundDirectoryEntryType.Root)
            {
                throw Invalid("The compound file contains more than one root storage.");
            }
        }

        foreach (var entry in entries)
        {
            if (entry.Type == CompoundDirectoryEntryType.Empty)
            {
                continue;
            }

            ValidateDirectoryId(entry.LeftSiblingId, entries.Count, "left sibling");
            ValidateDirectoryId(entry.RightSiblingId, entries.Count, "right sibling");
            ValidateDirectoryId(entry.ChildId, entries.Count, "child");

            if (entry.Type == CompoundDirectoryEntryType.Stream && entry.ChildId != NoStream)
            {
                throw Invalid("A compound file stream directory entry has a child.");
            }
        }

        return entries;
    }

    private CompoundDirectoryEntry BuildPaths(IReadOnlyList<CompoundDirectoryEntry> entries)
    {
        var root = entries[0];
        if (root.LeftSiblingId != NoStream || root.RightSiblingId != NoStream)
        {
            throw Invalid("The compound file root storage has a sibling.");
        }

        var referenced = new HashSet<uint> { 0 };
        var pending = new Stack<(
            uint Id,
            string ParentPath,
            int StorageDepth,
            int SiblingDepth)>();
        var totalRetainedPathCharacters = 0;
        AddDirectoryReference(
            root.ChildId,
            string.Empty,
            storageDepth: 0,
            siblingDepth: 1,
            entries,
            referenced,
            pending);

        while (pending.Count > 0)
        {
            var (id, parentPath, storageDepth, siblingDepth) = pending.Pop();
            var entry = entries[checked((int)id)];
            if (entry.Type == CompoundDirectoryEntryType.Empty)
            {
                throw Invalid("A compound file storage tree references an empty directory entry.");
            }

            var separatorLength = parentPath.Length == 0 ? 0 : 1;
            if (parentPath.Length > MaxPathCharacters - separatorLength - entry.Name.Length)
            {
                throw Invalid("A compound file directory path exceeds the character limit.");
            }

            var pathLength = checked(parentPath.Length + separatorLength + entry.Name.Length);
            if (totalRetainedPathCharacters > MaxTotalRetainedPathCharacters - pathLength)
            {
                throw Invalid("The compound file directory paths exceed the aggregate character limit.");
            }

            totalRetainedPathCharacters = checked(totalRetainedPathCharacters + pathLength);
            var path = separatorLength == 0 ? entry.Name : parentPath + "/" + entry.Name;
            if (!_entriesByPath.TryAdd(path, entry))
            {
                throw Invalid("The compound file contains a duplicate directory path.");
            }

            AddDirectoryReference(
                entry.LeftSiblingId,
                parentPath,
                storageDepth,
                siblingDepth + 1,
                entries,
                referenced,
                pending);
            AddDirectoryReference(
                entry.RightSiblingId,
                parentPath,
                storageDepth,
                siblingDepth + 1,
                entries,
                referenced,
                pending);
            if (entry.Type == CompoundDirectoryEntryType.Storage)
            {
                AddDirectoryReference(
                    entry.ChildId,
                    path,
                    storageDepth + 1,
                    siblingDepth: 1,
                    entries,
                    referenced,
                    pending);
            }
        }

        var populatedEntryCount = entries.Count(entry => entry.Type != CompoundDirectoryEntryType.Empty);
        if (referenced.Count != populatedEntryCount)
        {
            throw Invalid("The compound file contains an unreachable directory entry.");
        }

        return root;
    }

    private static void AddDirectoryReference(
        uint id,
        string parentPath,
        int storageDepth,
        int siblingDepth,
        IReadOnlyList<CompoundDirectoryEntry> entries,
        HashSet<uint> referenced,
        Stack<(uint Id, string ParentPath, int StorageDepth, int SiblingDepth)> pending)
    {
        if (id == NoStream)
        {
            return;
        }

        if (id >= entries.Count || !referenced.Add(id))
        {
            throw Invalid("The compound file directory tree contains a cycle or reused entry.");
        }

        if (siblingDepth > MaxSiblingTreeDepth)
        {
            throw Invalid("A compound file sibling tree exceeds the depth limit.");
        }

        if (storageDepth > MaxStorageDepth ||
            (storageDepth == MaxStorageDepth &&
             entries[checked((int)id)].Type == CompoundDirectoryEntryType.Storage))
        {
            throw Invalid("A compound file storage tree exceeds the depth limit.");
        }

        pending.Push((id, parentPath, storageDepth, siblingDepth));
    }

    private byte[] ReadRegularStream(CompoundDirectoryEntry entry, int streamLength)
    {
        var expectedSectors = GetExpectedSectorCount(
            entry.StreamSize,
            _sectorSize,
            _physicalSectorCount,
            "stream");
        var chain = WalkRegularChain(
            entry.StartSector,
            expectedSectors,
            _physicalSectorCount,
            "stream");
        var result = new byte[streamLength];
        var bytesRemaining = result.Length;
        var destinationOffset = 0;

        foreach (var sectorId in chain)
        {
            var bytesToRead = Math.Min(bytesRemaining, _sectorSize);
            ReadAt(SectorOffset(sectorId), result.AsSpan(destinationOffset, bytesToRead));
            destinationOffset += bytesToRead;
            bytesRemaining -= bytesToRead;
        }

        return result;
    }

    private byte[] ReadMiniStream(CompoundDirectoryEntry entry, int streamLength)
    {
        var expectedSectors = GetExpectedSectorCount(
            entry.StreamSize,
            MiniSectorSize,
            MaxChainEntries,
            "mini stream");
        var chain = WalkMiniChain(entry.StartSector, expectedSectors);
        var result = new byte[streamLength];
        var bytesRemaining = result.Length;
        var destinationOffset = 0;

        foreach (var miniSectorId in chain)
        {
            var miniOffset = checked((ulong)miniSectorId * MiniSectorSize);
            var bytesToRead = Math.Min(bytesRemaining, MiniSectorSize);
            if (miniOffset > _rootMiniStreamSize ||
                (ulong)bytesToRead > _rootMiniStreamSize - miniOffset)
            {
                throw Invalid("A mini-sector extends beyond the root mini stream.");
            }

            var rootSectorIndex = checked((int)(miniOffset / (ulong)_sectorSize));
            var offsetWithinRootSector = checked((int)(miniOffset % (ulong)_sectorSize));
            if (rootSectorIndex >= _rootMiniStreamChain.Count)
            {
                throw Invalid("A mini-sector maps outside the root mini stream chain.");
            }

            ReadAt(
                checked(SectorOffset(_rootMiniStreamChain[rootSectorIndex]) + offsetWithinRootSector),
                result.AsSpan(destinationOffset, bytesToRead));
            destinationOffset += bytesToRead;
            bytesRemaining -= bytesToRead;
        }

        return result;
    }

    private List<uint> WalkRegularChain(
        uint startSector,
        int? expectedSectorCount,
        int maximumSectorCount,
        string description)
    {
        var maximum = Math.Min(maximumSectorCount, MaxChainEntries);
        if (expectedSectorCount > maximum)
        {
            throw Invalid($"The compound file {description} chain exceeds the entry limit.");
        }

        if (expectedSectorCount == 0)
        {
            if (startSector != EndOfChain)
            {
                throw Invalid($"The empty compound file {description} has a sector chain.");
            }

            return [];
        }

        var result = new List<uint>(expectedSectorCount ?? Math.Min(maximum, 16));
        var visited = new HashSet<uint>();
        var current = startSector;

        while (true)
        {
            if (result.Count >= maximum)
            {
                throw Invalid($"The compound file {description} chain exceeds the entry limit.");
            }

            ValidatePhysicalSector(current, description);
            if (!visited.Add(current))
            {
                throw Invalid($"The compound file {description} chain contains a cycle.");
            }

            result.Add(current);
            var next = GetFatEntry(current);
            if (next == EndOfChain)
            {
                break;
            }

            if (expectedSectorCount.HasValue && result.Count >= expectedSectorCount.Value)
            {
                throw Invalid($"The compound file {description} chain is longer than its size.");
            }

            ValidatePhysicalSector(next, description);
            current = next;
        }

        if (expectedSectorCount.HasValue && result.Count != expectedSectorCount.Value)
        {
            throw Invalid($"The compound file {description} chain is shorter than its size.");
        }

        return result;
    }

    private List<uint> WalkMiniChain(uint startSector, int expectedSectorCount)
    {
        if (expectedSectorCount == 0)
        {
            if (startSector != EndOfChain)
            {
                throw Invalid("An empty compound file mini stream has a sector chain.");
            }

            return [];
        }

        var result = new List<uint>(expectedSectorCount);
        var visited = new HashSet<uint>();
        var current = startSector;

        while (true)
        {
            if (result.Count >= expectedSectorCount || result.Count >= MaxChainEntries)
            {
                throw Invalid("The compound file mini stream chain is longer than its size.");
            }

            if (current >= _miniFat.Length || !visited.Add(current))
            {
                throw Invalid("The compound file mini stream chain is cyclic or out of range.");
            }

            result.Add(current);
            var next = _miniFat[current];
            if (next == EndOfChain)
            {
                break;
            }

            if (next is FreeSector or FatSector or DifatSector || next >= _miniFat.Length)
            {
                throw Invalid("The compound file mini stream chain contains an invalid marker.");
            }

            current = next;
        }

        if (result.Count != expectedSectorCount)
        {
            throw Invalid("The compound file mini stream chain is shorter than its size.");
        }

        return result;
    }

    private byte[] ReadWholeSectors(List<uint> chain, int byteLimit, string description)
    {
        var byteCount = checked((long)chain.Count * _sectorSize);
        var bytes = new byte[InputGuard.CheckedLength(byteCount, byteLimit, $"The compound file {description}")];
        for (var index = 0; index < chain.Count; index++)
        {
            ReadSector(chain[index], bytes.AsSpan(index * _sectorSize, _sectorSize));
        }

        return bytes;
    }

    private void ReadSector(uint sectorId, Span<byte> destination)
    {
        if (destination.Length != _sectorSize)
        {
            throw new ArgumentException("A sector read must use the configured sector size.", nameof(destination));
        }

        ValidatePhysicalSector(sectorId, "sector");
        ReadAt(SectorOffset(sectorId), destination);
    }

    private long SectorOffset(uint sectorId) => checked(((long)sectorId + 1) * _sectorSize);

    private uint GetFatEntry(uint sectorId)
    {
        if (sectorId >= _fat.Length)
        {
            throw Invalid("A compound file sector has no FAT entry.");
        }

        return _fat[sectorId];
    }

    private void ValidatePhysicalSector(uint sectorId, string description)
    {
        if (sectorId >= _physicalSectorCount)
        {
            throw Invalid($"The compound file {description} sector ID is outside the file.");
        }
    }

    private static void ValidateDirectoryId(uint id, int entryCount, string description)
    {
        if (id != NoStream && id >= entryCount)
        {
            throw Invalid($"A compound file directory {description} ID is out of range.");
        }
    }

    private static int GetExpectedSectorCount(
        ulong streamSize,
        int sectorSize,
        int maximumSectorCount,
        string description)
    {
        var count = streamSize == 0
            ? 0
            : ((streamSize - 1) / checked((ulong)sectorSize)) + 1;
        if (count > checked((ulong)maximumSectorCount) || count > MaxChainEntries)
        {
            throw Invalid($"The compound file {description} size exceeds its sector bounds.");
        }

        return checked((int)count);
    }

    private void ReadAt(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset > _stream.Length - destination.Length)
        {
            throw Invalid("The compound file is truncated.");
        }

        _stream.Position = offset;
        var bytesRead = 0;
        while (bytesRead < destination.Length)
        {
            var read = _stream.Read(destination[bytesRead..]);
            if (read == 0)
            {
                throw Invalid("The compound file is truncated.");
            }

            bytesRead += read;
        }
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

    private static InvalidDataException Invalid(string message) => new(message);

    private static InvalidDataException Invalid(string message, Exception innerException) =>
        new(message, innerException);
}
