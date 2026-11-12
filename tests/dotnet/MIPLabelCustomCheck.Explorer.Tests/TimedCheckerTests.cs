using System.IO;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.Tests;

public sealed class TimedCheckerTests
{
    [Fact]
    public void Measure_successful_check_returns_result_without_error()
    {
        var timed = new TimedChecker((path, options) => new MipLabelChecker().Check(path, options));

        var measured = timed.Measure(FixturePaths.Get(".docx"));

        Assert.Null(measured.Error);
        Assert.True(measured.IsSuccess);
        Assert.NotNull(measured.Result);
        Assert.Equal(LabelStatus.Labeled, measured.Result!.Status);
    }

    [Fact]
    public void Measure_records_elapsed_time_for_successful_check()
    {
        var timed = new TimedChecker((path, options) => new MipLabelChecker().Check(path, options));

        var measured = timed.Measure(FixturePaths.Get(".png"));

        Assert.True(measured.Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public void Measure_captures_thrown_exception_instead_of_propagating()
    {
        var timed = new TimedChecker((_, _) => throw new IOException("boom"));

        var measured = timed.Measure(@"C:\nonexistent\anything.docx");

        Assert.Null(measured.Result);
        Assert.False(measured.IsSuccess);
        var error = Assert.IsType<IOException>(measured.Error);
        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public void Measure_records_elapsed_time_even_when_check_throws()
    {
        var timed = new TimedChecker((_, _) => throw new IOException("boom"));

        var measured = timed.Measure(@"C:\nonexistent\anything.docx");

        Assert.True(measured.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void Measure_captures_file_locked_exception_from_a_real_exclusive_lock()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mip-explorer-lock-{Guid.NewGuid():N}.png");
        File.Copy(FixturePaths.Get(".png"), path);

        try
        {
            var timed = new TimedChecker((path, options) => new MipLabelChecker().Check(path, options));

            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var measured = timed.Measure(path);

                Assert.Null(measured.Result);
                var error = Assert.IsType<MipLabelCheckException>(measured.Error);
                Assert.Equal(MipLabelCheckErrorReason.FileLocked, error.Reason);
                Assert.Equal(path, error.FilePath);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Constructor_null_delegate_throws() =>
        Assert.Throws<ArgumentNullException>(() => new TimedChecker(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Measure_empty_or_whitespace_path_throws(string path) =>
        Assert.Throws<ArgumentException>(
            () => new TimedChecker((_, _) => throw new InvalidOperationException()).Measure(path));
}
