namespace MIPLabelCustomCheck.Explorer.Scanning;

/// <summary>
/// Checks a list of files in parallel, reporting each file's outcome as it completes.
/// </summary>
/// <remarks>
/// Knows nothing about the UI thread: it reports through <see cref="IProgress{T}"/> and leaves
/// marshalling to the caller. Cancellation stops files that have not started rather than
/// interrupting one in flight, because <see cref="MipLabelChecker.Check(string)"/> takes no
/// cancellation token; cancelled files are reported with a null result instead of raising
/// <see cref="OperationCanceledException"/>.
/// </remarks>
public sealed class ScanSession
{
    private readonly TimedChecker _checker;

    public ScanSession(TimedChecker checker)
    {
        ArgumentNullException.ThrowIfNull(checker);
        _checker = checker;
    }

    /// <summary>
    /// Scans <paramref name="filePaths"/>, reporting one <see cref="ScanProgress"/> per file, and
    /// returns what the scan actually did.
    /// </summary>
    /// <param name="filePaths">The files to check.</param>
    /// <param name="progress">Receives one report per file, as each completes.</param>
    /// <param name="includeDetails">Whether each check also reads label metadata beyond the GUID.</param>
    /// <param name="cancellationToken">Stops files that have not started yet.</param>
    public Task<ScanSummary> RunAsync(
        IReadOnlyList<string> filePaths,
        IProgress<ScanProgress> progress,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        ArgumentNullException.ThrowIfNull(progress);

        return filePaths.Count == 0
            ? Task.FromResult(new ScanSummary(0, 0, 0))
            : Task.Run(
                () => Run(filePaths, progress, includeDetails, cancellationToken),
                CancellationToken.None);
    }

    private ScanSummary Run(
        IReadOnlyList<string> filePaths,
        IProgress<ScanProgress> progress,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        var processed = 0;
        var canceled = 0;
        var total = filePaths.Count;

        // ParallelOptions.CancellationToken은 일부러 설정하지 않는다. 설정하면 루프가
        // OperationCanceledException으로 중단되어 남은 파일이 보고되지 않는데,
        // UI는 그 파일들을 "취소됨"으로 표시해야 하므로 하나하나 보고가 필요하다.
        Parallel.ForEach(
            filePaths,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            filePath =>
            {
                TimedCheckResult? result = null;

                if (cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref canceled);
                }
                else
                {
                    result = _checker.Measure(filePath, includeDetails);
                }

                progress.Report(
                    new ScanProgress(filePath, result, Interlocked.Increment(ref processed), total));
            });

        return new ScanSummary(total - canceled, canceled, total);
    }
}
