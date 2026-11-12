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
}
