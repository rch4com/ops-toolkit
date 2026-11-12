using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests.TestData;

/// <summary>
/// In-memory <see cref="ICompoundFileView"/> used to exercise
/// <c>CompoundFileLabelReader</c> precedence rules without building real CFB binaries.
/// </summary>
internal sealed class FakeCompoundFileView : ICompoundFileView
{
    private readonly Dictionary<string, byte[]> _streams = new(StringComparer.Ordinal);
    private readonly HashSet<string> _paths = new(StringComparer.Ordinal);

    internal FakeCompoundFileView AddStream(string path, byte[] bytes)
    {
        _streams[path] = bytes;
        _paths.Add(path);
        return this;
    }

    internal FakeCompoundFileView AddStorage(string path)
    {
        _paths.Add(path);
        return this;
    }

    public bool Contains(string path) => _paths.Contains(path);

    public bool TryReadStream(string path, out byte[] bytes)
    {
        if (_streams.TryGetValue(path, out var found))
        {
            bytes = found;
            return true;
        }

        bytes = [];
        return false;
    }

    public void Dispose()
    {
    }
}
