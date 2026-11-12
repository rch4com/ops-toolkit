namespace MIPLabelCustomCheck.Explorer.Scanning;

/// <summary>One file's outcome within a folder scan, reported as the scan proceeds.</summary>
/// <param name="FilePath">The file this report is about.</param>
/// <param name="Result">
/// The file's timed check, or <see langword="null"/> when the scan was cancelled before this
/// file's check started. A null result means "not checked", not "no label".
/// </param>
/// <param name="Processed">How many files have been reported so far, including this one.</param>
/// <param name="Total">How many files the scan covers.</param>
public sealed record ScanProgress(
    string FilePath,
    TimedCheckResult? Result,
    int Processed,
    int Total);
