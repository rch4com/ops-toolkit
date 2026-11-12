using System.Collections.Concurrent;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.Tests;

/// <summary>
/// Collects progress reports synchronously on the reporting thread. Unlike
/// <see cref="Progress{T}"/> it does not post to a synchronization context, so a test can assert
/// on the reports as soon as the scan has finished.
/// </summary>
internal sealed class CollectingProgress : IProgress<ScanProgress>
{
    private readonly ConcurrentBag<ScanProgress> _reports = [];

    internal IReadOnlyCollection<ScanProgress> Reports => _reports;

    public void Report(ScanProgress value) => _reports.Add(value);
}
