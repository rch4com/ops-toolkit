using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.ViewModels;

/// <summary>Orchestrates the tree, list, and detail panes and owns the scan lifecycle.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly TimedChecker _checker;
    private readonly ScanSession _session;
    private readonly Action<string> _openFile;

    private CancellationTokenSource? _scanCancellation;
    private FolderTreeNodeViewModel? _selectedNode;
    private FileEntryViewModel? _selectedFile;

    // 현재 열려 있는 폴더. 트리 노드와 따로 둔다: 주소창으로 트리에 없는 경로(네트워크 경로
    // 등)를 열 수도 있고, 그때도 다시검사와 옵션 변경이 동작해야 하기 때문이다.
    private string? _currentFolder;
    private IReadOnlySet<string> _extensions = LabelableExtensions.Default;
    private string _extensionsText = LabelableExtensions.Format(LabelableExtensions.Default);
    private string _pathText = string.Empty;
    private bool _isPathValid = true;
    private bool _isScanning;
    private bool _isRemeasuring;
    private bool _includeDetailsByDefault;
    private bool _remeasureWithDetails = true;
    private string _statusText = "폴더를 선택하세요.";

    /// <param name="checker">Times and performs the label checks.</param>
    /// <param name="openFile">
    /// Opens a file in whatever program is associated with it. Injectable so tests can assert a
    /// double-click asks for the right path without actually launching Word.
    /// </param>
    public MainViewModel(TimedChecker checker, Action<string>? openFile = null)
    {
        ArgumentNullException.ThrowIfNull(checker);
        _checker = checker;
        _openFile = openFile ?? ShellOpen;
        _session = new ScanSession(checker);

        Drives = new ObservableCollection<FolderTreeNodeViewModel>(
            FolderTreeNodeViewModel.CreateDriveRoots());
        Files = [];

        NavigateCommand = new RelayCommand(() => _ = NavigateAsync());
        RescanCommand = new RelayCommand(
            () => _ = OpenFolderAsync(_currentFolder!),
            () => !_isScanning && _currentFolder is not null);
        StopCommand = new RelayCommand(Stop, () => _isScanning);
        RemeasureCommand = new RelayCommand(
            () => _ = RemeasureAsync(),
            () => !_isScanning && !_isRemeasuring && _selectedFile is not null);
        ResetExtensionsCommand = new RelayCommand(
            () => ExtensionsText = LabelableExtensions.Format(LabelableExtensions.Default));
        OpenFileCommand = new RelayCommand(OpenSelectedFile, () => _selectedFile is not null);
    }

    /// <summary>
    /// Hands the file to the shell, which launches whatever program is associated with its
    /// extension — the same thing double-clicking in Explorer does.
    /// </summary>
    /// <remarks>
    /// <see cref="ProcessStartInfo.UseShellExecute"/> must be true: without it .NET tries to
    /// execute the file itself, which fails for anything that is not an executable.
    /// </remarks>
    private static void ShellOpen(string filePath) =>
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true })?.Dispose();

    public ObservableCollection<FolderTreeNodeViewModel> Drives { get; }

    public ObservableCollection<FileEntryViewModel> Files { get; }

    public RelayCommand NavigateCommand { get; }

    public RelayCommand RescanCommand { get; }

    public RelayCommand StopCommand { get; }

    public RelayCommand RemeasureCommand { get; }

    public RelayCommand ResetExtensionsCommand { get; }

    /// <summary>Opens the selected file in its associated program. Bound to double-click.</summary>
    public RelayCommand OpenFileCommand { get; }

    /// <summary>
    /// The extensions a scan checks, as an editable comma-separated list. Defaults to what
    /// Microsoft Purview can label, plus <c>.pfile</c> — see <see cref="LabelableExtensions"/>.
    /// Files outside the set are listed but not checked, and can still be checked by clicking one.
    /// </summary>
    public string ExtensionsText
    {
        get => _extensionsText;
        set
        {
            if (Set(ref _extensionsText, value))
            {
                _extensions = LabelableExtensions.Parse(value);
                Raise(nameof(ExtensionCount));
            }
        }
    }

    /// <summary>How many extensions the scan currently targets.</summary>
    public int ExtensionCount => _extensions.Count;

    /// <summary>
    /// Whether a folder scan reads each label's supplementary metadata. Off by default: a scan
    /// answers "which files are labelled", and the details are only worth their parsing cost once
    /// you are looking at one file. Changing this re-scans the current folder so the effect —
    /// including on the timings — is visible immediately.
    /// </summary>
    public bool IncludeDetailsByDefault
    {
        get => _includeDetailsByDefault;
        set
        {
            if (Set(ref _includeDetailsByDefault, value) && _currentFolder is not null)
            {
                _ = OpenFolderAsync(_currentFolder);
            }
        }
    }

    /// <summary>
    /// Whether the detail pane's re-measure reads supplementary metadata. Separate from
    /// <see cref="IncludeDetailsByDefault"/> so one file can be inspected in either mode without
    /// disturbing the scan, and so the two timings can be compared on the same file.
    /// </summary>
    public bool RemeasureWithDetails
    {
        get => _remeasureWithDetails;
        set => Set(ref _remeasureWithDetails, value);
    }

    public FolderTreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (Set(ref _selectedNode, value))
            {
                RescanCommand.RaiseCanExecuteChanged();

                if (value is { Path.Length: > 0 })
                {
                    PathText = value.Path;
                    _ = OpenFolderAsync(value.Path);
                }
            }
        }
    }

    public FileEntryViewModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (Set(ref _selectedFile, value))
            {
                RemeasureCommand.RaiseCanExecuteChanged();
                OpenFileCommand.RaiseCanExecuteChanged();
                _ = CheckSelectedIfNotYetCheckedAsync();
            }
        }
    }

    public string PathText
    {
        get => _pathText;
        set => Set(ref _pathText, value);
    }

    /// <summary>False after the address bar was given a path that does not exist.</summary>
    public bool IsPathValid
    {
        get => _isPathValid;
        private set => Set(ref _isPathValid, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (Set(ref _isScanning, value))
            {
                StopCommand.RaiseCanExecuteChanged();
                RescanCommand.RaiseCanExecuteChanged();
                RemeasureCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    /// <summary>
    /// Opens the selected file in its associated program. A file with no association, or one the
    /// user declines to open, reports on the status bar rather than taking the app down.
    /// </summary>
    public void OpenSelectedFile()
    {
        if (_selectedFile is null)
        {
            return;
        }

        var name = _selectedFile.Name;

        try
        {
            _openFile(_selectedFile.FilePath);
            StatusText = $"{name} 을(를) 연결된 프로그램으로 열었습니다.";
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException
                or IOException or UnauthorizedAccessException)
        {
            StatusText = $"{name} 을(를) 열 수 없습니다: {exception.Message}";
        }
    }

    /// <summary>Cancels any running scan.</summary>
    public void Stop()
    {
        if (!_isScanning || !TryCancel(_scanCancellation))
        {
            return;
        }

        StatusText = "중지 요청됨 — 진행 중인 검사 하나가 끝나면 멈춥니다.";
    }

    /// <summary>Lists <paramref name="folderPath"/> and scans every file in it.</summary>
    /// <remarks>
    /// Owns the scan's <see cref="CancellationTokenSource"/> for exactly as long as the scan
    /// runs. The source is disposed only after the scan it belongs to has finished, and the
    /// field is cleared first, so a later navigation never touches a disposed source.
    /// </remarks>
    public async Task OpenFolderAsync(string folderPath)
    {
        TryCancel(_scanCancellation);

        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;

        try
        {
            await ScanFolderAsync(folderPath, cancellation.Token);
        }
        finally
        {
            // 이 스캔이 아직 최신이라면 정리한다. 그 사이 다른 폴더가 열렸다면
            // 그쪽이 최신이므로 IsScanning과 필드를 건드리지 않는다.
            if (ReferenceEquals(_scanCancellation, cancellation))
            {
                _scanCancellation = null;
                IsScanning = false;
            }

            cancellation.Dispose();
        }
    }

    /// <summary>Cancels <paramref name="source"/> unless it is null or already disposed.</summary>
    private static bool TryCancel(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return false;
        }

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // 이미 끝나고 정리된 스캔이다. 취소할 것이 없다.
            return false;
        }
    }

    private async Task ScanFolderAsync(string folderPath, CancellationToken cancellationToken)
    {
        Files.Clear();
        SelectedFile = null;

        string[] filePaths;
        try
        {
            filePaths = Directory.GetFiles(folderPath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or ArgumentException)
        {
            StatusText = $"폴더를 읽을 수 없습니다: {exception.Message}";
            return;
        }

        _currentFolder = folderPath;
        RescanCommand.RaiseCanExecuteChanged();

        foreach (var filePath in filePaths)
        {
            long size;
            try
            {
                size = new FileInfo(filePath).Length;
            }
            catch (IOException)
            {
                size = 0;
            }

            Files.Add(new FileEntryViewModel(filePath, size));
        }

        if (Files.Count == 0)
        {
            StatusText = "이 폴더에는 파일이 없습니다.";
            return;
        }

        // 설정된 확장자만 검사한다. 나머지는 목록에 남기되 "대상 외"로 두어,
        // 탐색기처럼 폴더 전체가 보이면서도 스캔은 레이블이 있을 법한 파일에만 시간을 쓴다.
        var targets = new List<string>();
        var skipped = 0;

        foreach (var file in Files)
        {
            if (LabelableExtensions.Includes(_extensions, file.FilePath))
            {
                targets.Add(file.FilePath);
            }
            else
            {
                file.MarkSkipped();
                skipped++;
            }
        }

        if (targets.Count == 0)
        {
            StatusText = $"검사 대상 없음 — {Files.Count}개 모두 설정된 확장자 밖입니다.";
            return;
        }

        var byPath = Files.ToDictionary(file => file.FilePath, StringComparer.OrdinalIgnoreCase);

        IsScanning = true;
        StatusText = $"검사 중 0/{targets.Count}";

        // 진행 표시가 완료 메시지를 덮어쓰지 못하게 한다. Progress<T>는 보고를 비동기로
        // 전달하므로 스캔 태스크가 끝난 뒤에도 콜백이 큐에 남아 있을 수 있고, 그것이 나중에
        // 실행되면 상태바가 "검사 중 N/N"에 멈춘 채 끝난다. 항목 갱신은 늦게 와도 유효하므로
        // 그대로 두고, 상태바 문구만 막는다.
        var finished = false;

        var progress = new Progress<ScanProgress>(report =>
        {
            if (byPath.TryGetValue(report.FilePath, out var entry))
            {
                entry.Apply(report.Result);
            }

            if (!finished)
            {
                StatusText = $"검사 중 {report.Processed}/{report.Total}";
            }
        });

        // 요약도 스캔이 직접 센 값을 쓴다. 같은 이유로 콜백에서 세면 숫자가 적게 나온다.
        var summary = await _session.RunAsync(
            targets, progress, _includeDetailsByDefault, cancellationToken);
        finished = true;

        var skippedNote = skipped > 0 ? $", 대상 외 {skipped}개 제외" : string.Empty;
        StatusText = summary.Canceled > 0
            ? $"{summary.Total}개 중 {summary.Checked}개 검사됨 ({summary.Canceled}개 취소됨{skippedNote})"
            : $"{summary.Total}개 검사 완료{skippedNote}";
    }

    private async Task NavigateAsync()
    {
        var target = PathText?.Trim() ?? string.Empty;

        if (target.Length == 0 || !Directory.Exists(target))
        {
            IsPathValid = false;
            return;
        }

        IsPathValid = true;

        foreach (var drive in Drives)
        {
            var node = drive.FindOrExpandTo(target);
            if (node is not null)
            {
                SelectedNode = node;
                return;
            }
        }

        // 트리에 없는 경로(네트워크 경로 등)라도 목록은 연다.
        await OpenFolderAsync(target);
    }

    /// <summary>
    /// Checks a file the scan never reached. Runs only when no scan is in flight, so the
    /// measurement is a standalone one.
    /// </summary>
    private async Task CheckSelectedIfNotYetCheckedAsync()
    {
        if (_isScanning || _selectedFile is null)
        {
            return;
        }

        // 대상 외 파일도 클릭하면 검사한다. 라이브러리는 확장자가 아니라 내용으로 형식을
        // 판별하므로, 확장자를 속인 파일은 이 경로에서 정체가 드러난다.
        if (_selectedFile.State is not (CheckState.Canceled or CheckState.Pending or CheckState.Skipped))
        {
            return;
        }

        // 클릭으로 채워지는 값은 목록의 다른 행과 같은 조건이어야 비교가 되므로 기본 옵션을 쓴다.
        await RunSingleCheckAsync(
            _selectedFile, applyAsRemeasurement: false, _includeDetailsByDefault);
    }

    private async Task RemeasureAsync()
    {
        if (_selectedFile is null)
        {
            return;
        }

        // 재측정은 상세 패널의 체크박스를 따른다 — 같은 파일을 두 모드로 재보아
        // 부가 정보의 비용을 직접 확인하는 것이 이 버튼의 용도다.
        await RunSingleCheckAsync(
            _selectedFile, applyAsRemeasurement: true, _remeasureWithDetails);
    }

    private async Task RunSingleCheckAsync(
        FileEntryViewModel entry,
        bool applyAsRemeasurement,
        bool includeDetails)
    {
        _isRemeasuring = true;
        RemeasureCommand.RaiseCanExecuteChanged();
        entry.MarkRunning();

        var measured = await Task.Run(() => _checker.Measure(entry.FilePath, includeDetails));

        if (applyAsRemeasurement)
        {
            entry.ApplyRemeasurement(measured);
        }
        else
        {
            entry.Apply(measured);
        }

        _isRemeasuring = false;
        RemeasureCommand.RaiseCanExecuteChanged();
    }
}
