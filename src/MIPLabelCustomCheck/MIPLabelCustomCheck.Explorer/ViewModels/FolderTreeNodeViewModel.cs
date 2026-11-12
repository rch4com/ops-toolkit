using System.Collections.ObjectModel;
using System.IO;

namespace MIPLabelCustomCheck.Explorer.ViewModels;

/// <summary>
/// One folder in the tree. Children load on first expansion so opening a drive stays cheap.
/// </summary>
/// <remarks>
/// This class has its own <see cref="Path"/> property, which shadows <see cref="System.IO.Path"/>
/// inside it — hence the fully-qualified calls.
/// </remarks>
public sealed class FolderTreeNodeViewModel : ObservableObject
{
    private static readonly FolderTreeNodeViewModel Placeholder = new(string.Empty, "…");

    private bool _isExpanded;
    private bool _isSelected;
    private bool _isAccessible = true;
    private bool _childrenLoaded;

    public FolderTreeNodeViewModel(string path, string displayName)
    {
        Path = path;
        DisplayName = displayName;
        Children = [];

        if (path.Length > 0)
        {
            // 실제 자식은 확장 시점에 읽는다. 자리표시자가 있어야 확장 화살표가 보인다.
            Children.Add(Placeholder);
        }
    }

    public string Path { get; }

    public string DisplayName { get; }

    public ObservableCollection<FolderTreeNodeViewModel> Children { get; }

    /// <summary>False when the folder cannot be listed (access denied). The node stays visible.</summary>
    public bool IsAccessible
    {
        get => _isAccessible;
        private set => Set(ref _isAccessible, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value) && value)
            {
                LoadChildren();
            }
        }
    }

    /// <summary>
    /// Whether the tree highlights this node. Bound two-way, so setting it from the view model —
    /// as <see cref="FindOrExpandTo"/> does after a path is typed — moves the tree's own selection
    /// to this node instead of leaving it expanded but with nothing highlighted.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>Ready drives only — an empty card reader would otherwise throw on enumeration.</summary>
    public static IReadOnlyList<FolderTreeNodeViewModel> CreateDriveRoots() =>
        DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => new FolderTreeNodeViewModel(drive.RootDirectory.FullName, drive.Name))
            .ToList();

    /// <summary>
    /// Expands the tree down to <paramref name="targetPath"/>, selects that node, and returns it;
    /// returns null when the path is not under this node.
    /// </summary>
    public FolderTreeNodeViewModel? FindOrExpandTo(string targetPath)
    {
        if (Path.Length == 0 || !IsUnder(targetPath, Path))
        {
            return null;
        }

        if (string.Equals(Normalize(targetPath), Normalize(Path), StringComparison.OrdinalIgnoreCase))
        {
            // 목적지 노드는 펼치지 않는다. 하위 폴더를 미리 읽을 이유가 없고,
            // 사용자가 직접 화살표를 눌러 펼치는 편이 탐색기와 같다.
            IsSelected = true;
            return this;
        }

        IsExpanded = true;

        foreach (var child in Children)
        {
            var found = child.FindOrExpandTo(targetPath);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string Normalize(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));

    private static bool IsUnder(string candidate, string ancestor)
    {
        try
        {
            return Normalize(candidate).StartsWith(Normalize(ancestor), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            // 잘못된 형식의 경로는 어느 노드에도 속하지 않는 것으로 취급한다.
            return false;
        }
    }

    private void LoadChildren()
    {
        if (_childrenLoaded)
        {
            return;
        }

        _childrenLoaded = true;
        Children.Clear();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(Path).Order(StringComparer.OrdinalIgnoreCase))
            {
                Children.Add(
                    new FolderTreeNodeViewModel(directory, System.IO.Path.GetFileName(directory)));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // 접근할 수 없는 폴더는 빈 노드로 남긴다. 사용자에게 예외를 던지지 않는다.
            IsAccessible = false;
        }
    }
}
