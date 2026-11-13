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

    [Fact]
    public async Task RunAsyncWithOutputCsvAndOutputJsonWritesReportFilesWithUtf8Bom()
    {
        string sampleDocx = FixturePaths.Get(".docx");
        string tempDir = Path.Combine(Path.GetTempPath(), $"MipCliTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string csvPath = Path.Combine(tempDir, "report.csv");
            string jsonPath = Path.Combine(tempDir, "report.json");

            bool parsed = CliOptions.TryParse(
                ["--path", sampleDocx, "--output-csv", csvPath, "--output-json", jsonPath],
                out CliOptions options,
                out string? error);

            Assert.True(parsed);
            Assert.Null(error);
            Assert.Equal(csvPath, options.OutputCsvPath);
            Assert.Equal(jsonPath, options.OutputJsonPath);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = await CliRunner.RunAsync(options, new MipLabelChecker(), outWriter, errWriter);

            Assert.Equal(CliRunner.ExitSuccess, exitCode);
            Assert.True(File.Exists(csvPath));
            Assert.True(File.Exists(jsonPath));

            byte[] csvBytes = await File.ReadAllBytesAsync(csvPath);
            Assert.True(csvBytes.Length >= 3 && csvBytes[0] == 0xEF && csvBytes[1] == 0xBB && csvBytes[2] == 0xBF);

            string csvText = await File.ReadAllTextAsync(csvPath);
            Assert.Contains("FilePath,FileName,FileSizeBytes,Status,Protection,ProtectionDetail,LabelIds,Sources,ElapsedMilliseconds,ErrorMessage", csvText, StringComparison.Ordinal);
            Assert.Contains("Labeled", csvText, StringComparison.Ordinal);

            string jsonText = await File.ReadAllTextAsync(jsonPath);
            Assert.Contains("\"labeledCount\": 1", jsonText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
