using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace MIPLabelCustomCheck.Explorer.Scanning;

/// <summary>
/// Times one label check and captures its outcome, so a failing file surfaces as data rather
/// than an exception the caller must handle.
/// </summary>
/// <remarks>
/// <para>
/// Takes the check as a delegate rather than a <see cref="MipLabelChecker"/> because that type
/// is sealed and cannot be substituted in tests.
/// </para>
/// <para>
/// The first call into each format's reader compiles that reader's code path, costing 10-25 ms
/// that has nothing to do with the file. Left alone it dominates the reported time and makes
/// identical formats look orders of magnitude apart. So the first file of each extension is
/// checked once with its timing discarded, then checked again for the reported value. Keyed by
/// extension because the library detects format from content and does not report which one it
/// found; an extension is a good enough proxy for deciding when to warm up.
/// </para>
/// </remarks>
public sealed class TimedChecker
{
    private readonly Func<string, LabelCheckOptions, LabelCheckResult> _check;
    private readonly ConcurrentDictionary<string, bool> _warmedPaths =
        new(StringComparer.OrdinalIgnoreCase);

    public TimedChecker(Func<string, LabelCheckOptions, LabelCheckResult> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        _check = check;
    }

    /// <summary>Checks <paramref name="filePath"/> once and reports the outcome with its timing.</summary>
    /// <param name="filePath">The file to check.</param>
    /// <param name="includeDetails">
    /// Whether to also read each label's supplementary metadata. Costs extra parsing, so the
    /// timing this reports is the timing of whichever mode was asked for — the point of making it
    /// switchable is being able to see that difference.
    /// </param>
    public TimedCheckResult Measure(string filePath, bool includeDetails = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var options = includeDetails ? LabelCheckOptions.WithDetails : LabelCheckOptions.Default;
        WarmUp(filePath, options, includeDetails);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = _check(filePath, options);
            stopwatch.Stop();
            return new TimedCheckResult(result, null, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new TimedCheckResult(null, exception, stopwatch.Elapsed);
        }
    }

    /// <remarks>
    /// Keyed by extension <em>and</em> the detail flag: reading details runs code the GUID-only
    /// path never touches, so that code JITs on its own first call. Warming only by extension
    /// would leave the first detailed check of each format carrying the JIT cost the warmup exists
    /// to remove.
    /// </remarks>
    private void WarmUp(string filePath, LabelCheckOptions options, bool includeDetails)
    {
        var key = $"{Path.GetExtension(filePath)}|{includeDetails}";
        if (!_warmedPaths.TryAdd(key, true))
        {
            return;
        }

        try
        {
            _check(filePath, options);
        }
        catch
        {
            // 워밍업의 결과도 예외도 버린다. 목적은 코드 경로를 밟는 것뿐이고,
            // 실패했다면 바로 뒤의 실제 측정이 같은 예외를 다시 잡아 보고한다.
        }
    }
}
