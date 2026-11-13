using System.IO;
using MIPLabelCustomCheck.Explorer.Scanning;
using MIPLabelCustomCheck.Explorer.ViewModels;

namespace MIPLabelCustomCheck.Explorer.Tests;

/// <summary>
/// Covers double-click-to-open. The opener is injected so these never actually launch Word.
/// </summary>
public sealed class MainViewModelOpenFileTests
{
    private static MainViewModel Create(Action<string> openFile) =>
        new(new TimedChecker((path, options) => new MipLabelChecker().Check(path, options)), openFile);

    [Fact]
    public async Task OpenSelectedFile_asks_the_shell_for_the_selected_path()
    {
        var opened = new List<string>();
        var viewModel = Create(opened.Add);
        await viewModel.OpenFolderAsync(Path.GetDirectoryName(FixturePaths.Get(".pdf"))!);
        viewModel.SelectedFile = viewModel.Files.First(f => f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

        viewModel.OpenSelectedFile();

        var path = Assert.Single(opened);
        Assert.Equal(viewModel.SelectedFile!.FilePath, path);
    }

    [Fact]
    public void OpenSelectedFile_does_nothing_when_no_file_is_selected()
    {
        var opened = new List<string>();
        var viewModel = Create(opened.Add);

        viewModel.OpenSelectedFile();

        Assert.Empty(opened);
    }

    [Fact]
    public void OpenFileCommand_is_disabled_until_a_file_is_selected() =>
        Assert.False(Create(_ => { }).OpenFileCommand.CanExecute(null));

    [Fact]
    public async Task OpenFileCommand_is_enabled_once_a_file_is_selected()
    {
        var viewModel = Create(_ => { });
        await viewModel.OpenFolderAsync(Path.GetDirectoryName(FixturePaths.Get(".pdf"))!);

        viewModel.SelectedFile = viewModel.Files.First();

        Assert.True(viewModel.OpenFileCommand.CanExecute(null));
    }

    /// <summary>
    /// A file with no associated program throws Win32Exception from the shell. That must reach the
    /// status bar, not the top of the stack.
    /// </summary>
    [Fact]
    public async Task OpenSelectedFile_reports_a_failure_to_open_instead_of_throwing()
    {
        var viewModel = Create(_ => throw new System.ComponentModel.Win32Exception("연결된 프로그램이 없습니다"));
        await viewModel.OpenFolderAsync(Path.GetDirectoryName(FixturePaths.Get(".pdf"))!);
        viewModel.SelectedFile = viewModel.Files.First();

        var exception = Record.Exception(viewModel.OpenSelectedFile);

        Assert.Null(exception);
        Assert.Contains("열 수 없습니다", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenSelectedFile_reports_success_on_the_status_bar()
    {
        var viewModel = Create(_ => { });
        await viewModel.OpenFolderAsync(Path.GetDirectoryName(FixturePaths.Get(".pdf"))!);
        viewModel.SelectedFile = viewModel.Files.First(f => f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

        viewModel.OpenSelectedFile();

        Assert.Contains("열었습니다", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportCsvCommand_writes_utf8_bom_csv_and_updates_status_text()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"MipExportTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string csvPath = Path.Combine(tempDir, "exported.csv");
            var viewModel = new MainViewModel(
                new TimedChecker((path, options) => new MipLabelChecker().Check(path, options)),
                openFile: _ => { },
                selectExportCsvPath: () => csvPath);

            Assert.False(viewModel.ExportCsvCommand.CanExecute(null));

            await viewModel.OpenFolderAsync(Path.GetDirectoryName(FixturePaths.Get(".pdf"))!);

            Assert.True(viewModel.ExportCsvCommand.CanExecute(null));
            viewModel.ExportCsvCommand.Execute(null);

            Assert.True(File.Exists(csvPath));
            byte[] bytes = await File.ReadAllBytesAsync(csvPath);
            Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

            string content = await File.ReadAllTextAsync(csvPath);
            Assert.Contains("FilePath,FileName,SizeBytes,StatusText,ProtectionText,LabelIds,SourcesText,ElapsedMs", content, StringComparison.Ordinal);
            Assert.Contains("레이블됨", content, StringComparison.Ordinal);
            Assert.Contains("CSV 내보내기 완료: exported.csv", viewModel.StatusText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task IncludeSubfolders_and_StatusFilter_enumerate_subdirectories_and_filter_items()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"MipSubfolderTest_{Guid.NewGuid():N}");
        string subDir = Path.Combine(tempRoot, "sub");
        Directory.CreateDirectory(subDir);

        try
        {
            string rootDocx = Path.Combine(tempRoot, "root.docx");
            string subPdf = Path.Combine(subDir, "nested.pdf");
            string skippedDll = Path.Combine(tempRoot, "ignored.dll");

            File.Copy(FixturePaths.Get(".docx"), rootDocx);
            File.Copy(FixturePaths.Get(".pdf"), subPdf);
            await File.WriteAllBytesAsync(skippedDll, [0x4D, 0x5A]);

            var viewModel = new MainViewModel(
                new TimedChecker((path, options) => new MipLabelChecker().Check(path, options)));

            await viewModel.OpenFolderAsync(tempRoot);
            Assert.Equal(2, viewModel.Files.Count);

            viewModel.IncludeSubfolders = true;
            await viewModel.OpenFolderAsync(tempRoot);
            Assert.Equal(3, viewModel.Files.Count);
            Assert.Equal(3, viewModel.FilteredFiles.Count);

            viewModel.SelectedStatusFilter = "레이블됨";
            Assert.Equal(2, viewModel.FilteredFiles.Count);

            viewModel.SelectedStatusFilter = "대상 외";
            Assert.Single(viewModel.FilteredFiles);

            viewModel.SelectedStatusFilter = "전체";
            Assert.Equal(3, viewModel.FilteredFiles.Count);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
