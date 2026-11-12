namespace MIPLabelCustomCheck.Internal.CompoundFile;

internal enum CompoundDirectoryEntryType : byte
{
    Empty = 0,
    Storage = 1,
    Stream = 2,
    Root = 5,
}

internal sealed class CompoundDirectoryEntry
{
    internal CompoundDirectoryEntry(
        int id,
        string name,
        CompoundDirectoryEntryType type,
        uint leftSiblingId,
        uint rightSiblingId,
        uint childId,
        uint startSector,
        ulong streamSize)
    {
        Id = id;
        Name = name;
        Type = type;
        LeftSiblingId = leftSiblingId;
        RightSiblingId = rightSiblingId;
        ChildId = childId;
        StartSector = startSector;
        StreamSize = streamSize;
    }

    internal int Id { get; }

    internal string Name { get; }

    internal CompoundDirectoryEntryType Type { get; }

    internal uint LeftSiblingId { get; }

    internal uint RightSiblingId { get; }

    internal uint ChildId { get; }

    internal uint StartSector { get; }

    internal ulong StreamSize { get; }
}
