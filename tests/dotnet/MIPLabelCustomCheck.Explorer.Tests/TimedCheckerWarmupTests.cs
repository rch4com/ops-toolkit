using System.Collections.Concurrent;
using System.IO;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.Tests;

public sealed class TimedCheckerWarmupTests
{
    [Fact]
    public void First_file_of_an_extension_is_checked_twice()
    {
        var calls = 0;
        var checker = new MipLabelChecker();
        var timed = new TimedChecker((path, options) =>
        {
            Interlocked.Increment(ref calls);
            return checker.Check(path, options);
        });

        timed.Measure(FixturePaths.Get(".docx"));

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Later_files_of_a_warmed_extension_are_checked_once()
    {
        var calls = 0;
        var checker = new MipLabelChecker();
        var timed = new TimedChecker((path, options) =>
        {
            Interlocked.Increment(ref calls);
            return checker.Check(path, options);
        });

        timed.Measure(FixturePaths.Get(".docx"));
        calls = 0;

        timed.Measure(FixturePaths.Get(".docx"));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Each_distinct_extension_warms_up_separately()
    {
        var calls = 0;
        var checker = new MipLabelChecker();
        var timed = new TimedChecker((path, options) =>
        {
            Interlocked.Increment(ref calls);
            return checker.Check(path, options);
        });

        timed.Measure(FixturePaths.Get(".docx"));
        timed.Measure(FixturePaths.Get(".xlsx"));

        // 확장자 2종 x (워밍업 1 + 측정 1)
        Assert.Equal(4, calls);
    }

    [Fact]
    public void Extension_comparison_ignores_case()
    {
        var lowerPath = FixturePaths.Get(".png");
        var upperPath = Path.Combine(Path.GetTempPath(), $"mip-explorer-case-{Guid.NewGuid():N}.PNG");
        File.Copy(lowerPath, upperPath);

        try
        {
            var calls = 0;
            var checker = new MipLabelChecker();
            var timed = new TimedChecker((path, options) =>
            {
                Interlocked.Increment(ref calls);
                return checker.Check(path, options);
            });

            timed.Measure(lowerPath);
            calls = 0;

            timed.Measure(upperPath);

            Assert.Equal(1, calls);
        }
        finally
        {
            File.Delete(upperPath);
        }
    }

    [Fact]
    public void Warmup_exception_does_not_escape_and_the_real_measurement_still_reports_it()
    {
        var timed = new TimedChecker((_, _) => throw new IOException("boom"));

        var measured = timed.Measure(@"C:\nonexistent\anything.rtf");

        Assert.False(measured.IsSuccess);
        Assert.IsType<IOException>(measured.Error);
    }

    [Fact]
    public void Warmed_measurement_excludes_jit_cost()
    {
        var timed = new TimedChecker((path, options) => new MipLabelChecker().Check(path, options));

        var measured = timed.Measure(FixturePaths.Get(".doc"));

        // 워밍업 없이는 20ms대가 나온다. 워밍업 후에는 1ms 미만이어야 한다.
        // 느린 CI 환경을 감안해 5ms로 넉넉히 잡아도 JIT 회귀는 20ms대라 걸러진다.
        Assert.True(
            measured.Elapsed < TimeSpan.FromMilliseconds(5),
            $"워밍업이 동작하지 않는 것으로 보인다. 실측: {measured.Elapsed.TotalMilliseconds:F3} ms");
    }

    [Fact]
    public void Parallel_measurement_of_the_same_extension_does_not_throw()
    {
        var checker = new MipLabelChecker();
        var timed = new TimedChecker((path, options) => checker.Check(path, options));
        var results = new ConcurrentBag<TimedCheckResult>();

        Parallel.For(0, 32, _ => results.Add(timed.Measure(FixturePaths.Get(".xlsx"))));

        Assert.Equal(32, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccess));
    }
}
