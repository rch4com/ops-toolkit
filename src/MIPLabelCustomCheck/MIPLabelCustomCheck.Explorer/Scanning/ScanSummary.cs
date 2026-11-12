namespace MIPLabelCustomCheck.Explorer.Scanning;

/// <summary>What a finished scan actually did, counted by the scan itself.</summary>
/// <param name="Checked">How many files were checked.</param>
/// <param name="Canceled">How many files were skipped because the scan was cancelled.</param>
/// <param name="Total">How many files the scan covered. Always <c>Checked + Canceled</c>.</param>
/// <remarks>
/// Returned rather than tallied from <see cref="IProgress{T}"/> callbacks: <see cref="Progress{T}"/>
/// delivers reports asynchronously, so callbacks can still be queued when the scan's task
/// completes, and a summary counted there would under-report.
/// </remarks>
public sealed record ScanSummary(int Checked, int Canceled, int Total);
