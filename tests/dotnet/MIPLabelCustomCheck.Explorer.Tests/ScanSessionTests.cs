using System.IO;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.Tests;

public sealed class ScanSessionTests
{
    private static readonly string[] AllFixtures =
    [
        FixturePaths.Get(".doc"),
        FixturePaths.Get(".docx"),
        FixturePaths.Get(".pdf"),
        FixturePaths.Get(".png"),
        FixturePaths.Get(".ppt"),
        FixturePaths.Get(".pptx"),
        FixturePaths.Get(".xls"),
        FixturePaths.Get(".xlsx")
    ];

    private static ScanSession RealSession() =>
        new(new TimedChecker((path, options) => new MipLabelChecker().Check(path, options)));

    [Fact]
    public async Task RunAsync_reports_every_file_once()
    {
        var progress = new CollectingProgress();

        await RealSession().RunAsync(AllFixtures, progress, includeDetails: false, CancellationToken.None);

        Assert.Equal(AllFixtures.Length, progress.Reports.Count);
        Assert.Equal(
            AllFixtures.OrderBy(p => p),
            progress.Reports.Select(r => r.FilePath).OrderBy(p => p));
    }

    [Fact]
    public async Task RunAsync_reports_results_for_every_file_when_not_cancelled()
    {
        var progress = new CollectingProgress();

        await RealSession().RunAsync(AllFixtures, progress, includeDetails: false, CancellationToken.None);

        Assert.All(progress.Reports, r => Assert.NotNull(r.Result));
        Assert.All(progress.Reports, r => Assert.Equal(LabelStatus.Labeled, r.Result!.Result!.Status));
    }

    [Fact]
    public async Task RunAsync_reports_total_and_increasing_processed_counts()
    {
        var progress = new CollectingProgress();

        await RealSession().RunAsync(AllFixtures, progress, includeDetails: false, CancellationToken.None);

        Assert.All(progress.Reports, r => Assert.Equal(AllFixtures.Length, r.Total));
        Assert.Equal(
            Enumerable.Range(1, AllFixtures.Length),
            progress.Reports.Select(r => r.Processed).OrderBy(p => p));
    }

    [Fact]
    public async Task RunAsync_marks_every_file_cancelled_when_cancelled_before_start()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var session = new ScanSession(
            new TimedChecker((_, _) => throw new InvalidOperationException("취소 후에는 호출되면 안 된다")));
        var progress = new CollectingProgress();

        await session.RunAsync(AllFixtures, progress, includeDetails: false, cts.Token);

        Assert.Equal(AllFixtures.Length, progress.Reports.Count);
        Assert.All(progress.Reports, r => Assert.Null(r.Result));
    }

    [Fact]
    public async Task RunAsync_does_not_throw_operation_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var progress = new CollectingProgress();

        var exception = await Record.ExceptionAsync(
            () => RealSession().RunAsync(AllFixtures, progress, includeDetails: false, cts.Token));

        Assert.Null(exception);
    }

    [Fact]
    public async Task RunAsync_continues_after_an_individual_file_throws()
    {
        var session = new ScanSession(new TimedChecker((path, options) =>
            path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? throw new IOException("boom")
                : new MipLabelChecker().Check(path, options)));
        var progress = new CollectingProgress();

        await session.RunAsync(AllFixtures, progress, includeDetails: false, CancellationToken.None);

        Assert.Equal(AllFixtures.Length, progress.Reports.Count);
        var failed = progress.Reports.Where(r => r.Result?.IsSuccess == false).ToList();
        var pdf = Assert.Single(failed);
        Assert.EndsWith(".pdf", pdf.FilePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AllFixtures.Length - 1, progress.Reports.Count(r => r.Result?.IsSuccess == true));
    }

    [Fact]
    public async Task RunAsync_empty_list_reports_nothing_and_completes()
    {
        var progress = new CollectingProgress();

        var summary = await RealSession().RunAsync([], progress, includeDetails: false, CancellationToken.None);

        Assert.Empty(progress.Reports);
        Assert.Equal(new ScanSummary(0, 0, 0), summary);
    }

    [Fact]
    public async Task RunAsync_summary_counts_every_file_as_checked_when_not_cancelled()
    {
        var summary = await RealSession()
            .RunAsync(AllFixtures, new CollectingProgress(), includeDetails: false, CancellationToken.None);

        Assert.Equal(new ScanSummary(AllFixtures.Length, 0, AllFixtures.Length), summary);
    }

    [Fact]
    public async Task RunAsync_summary_counts_every_file_as_cancelled_when_cancelled_before_start()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var session = new ScanSession(
            new TimedChecker((_, _) => throw new InvalidOperationException("취소 후에는 호출되면 안 된다")));

        var summary = await session.RunAsync(AllFixtures, new CollectingProgress(), includeDetails: false, cts.Token);

        Assert.Equal(new ScanSummary(0, AllFixtures.Length, AllFixtures.Length), summary);
    }

    /// <summary>
    /// The summary must not be tallied from progress callbacks: <see cref="Progress{T}"/> delivers
    /// them asynchronously, so a caller counting there under-reports. A progress sink that never
    /// counts anything still has to get exact totals.
    /// </summary>
    [Fact]
    public async Task RunAsync_summary_is_exact_and_independent_of_the_progress_sink()
    {
        var summary = await RealSession()
            .RunAsync(AllFixtures, new IgnoringProgress(), includeDetails: false, CancellationToken.None);

        Assert.Equal(AllFixtures.Length, summary.Total);
        Assert.Equal(summary.Total, summary.Checked + summary.Canceled);
    }

    private sealed class IgnoringProgress : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value)
        {
            // 일부러 아무것도 하지 않는다.
        }
    }
}
