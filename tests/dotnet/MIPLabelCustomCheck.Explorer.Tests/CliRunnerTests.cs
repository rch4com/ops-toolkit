using System.IO;
using MIPLabelCustomCheck.Explorer.Cli;

namespace MIPLabelCustomCheck.Explorer.Tests;

public class CliRunnerTests
{
    [Fact]
    public void TryParseWithNoArgsEnablesGuiMode()
    {
        bool ok = CliOptions.TryParse([], out CliOptions options, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(options.LaunchGui);
    }

    [Fact]
    public async Task RunAsyncWithDryRunDoesNotParseFileAndReturnsCandidate()
    {
        string sampleDocx = FixturePaths.Get(".docx");
        var options = new CliOptions
        {
            TargetPath = sampleDocx,
            IsDryRun = true,
            OutputJson = true
        };

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        int exitCode = await CliRunner.RunAsync(options, new MipLabelChecker(), outWriter, errWriter);

        Assert.Equal(CliRunner.ExitSuccess, exitCode);
        string output = outWriter.ToString();
        Assert.Contains("\"isDryRun\": true", output, StringComparison.Ordinal);
        Assert.Contains("DryRunCandidate", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncWithDetailsAndJsonOutputsLabeledFixtureMetadata()
    {
        string sampleDocx = FixturePaths.Get(".docx");
        var options = new CliOptions
        {
            TargetPath = sampleDocx,
            WithDetails = true,
            OutputJson = true
        };

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        int exitCode = await CliRunner.RunAsync(options, new MipLabelChecker(), outWriter, errWriter);

        Assert.Equal(CliRunner.ExitSuccess, exitCode);
        string output = outWriter.ToString();
        Assert.Contains("\"labeledCount\": 1", output, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"Labeled\"", output, StringComparison.Ordinal);
    }
}
