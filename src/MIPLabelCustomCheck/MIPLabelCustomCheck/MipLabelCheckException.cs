namespace MIPLabelCustomCheck;

/// <summary>The specific reason a <see cref="MipLabelCheckException"/> was thrown.</summary>
public enum MipLabelCheckErrorReason
{
    /// <summary>The file could not be opened for reading because another process holds an
    /// exclusive lock on it (a Windows sharing or lock violation).</summary>
    FileLocked
}

/// <summary>
/// Thrown by <see cref="MipLabelChecker.Check(string, LabelCheckOptions?)"/> when a file cannot be inspected because
/// it is exclusively locked by another process, rather than surfacing the underlying
/// <see cref="IOException"/> directly.
/// </summary>
public sealed class MipLabelCheckException : IOException
{
    internal MipLabelCheckException(
        MipLabelCheckErrorReason reason,
        string filePath,
        IOException innerException)
        : base($"The file cannot be inspected because it is locked: {filePath}", innerException)
    {
        Reason = reason;
        FilePath = filePath;
    }

    /// <summary>The specific reason the file could not be inspected.</summary>
    public MipLabelCheckErrorReason Reason { get; }

    /// <summary>The path of the file that could not be inspected.</summary>
    public string FilePath { get; }
}
