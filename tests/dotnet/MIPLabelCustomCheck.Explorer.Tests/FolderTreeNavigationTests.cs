using System.IO;
using MIPLabelCustomCheck.Explorer.ViewModels;

namespace MIPLabelCustomCheck.Explorer.Tests;

/// <summary>
/// Covers navigating to an absolute path: the tree must expand down to it and select it, so the
/// typed path and the tree stay connected.
/// </summary>
public sealed class FolderTreeNavigationTests
{
    private static FolderTreeNodeViewModel TempDriveRoot()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        return new FolderTreeNodeViewModel(root, root);
    }

    [Fact]
    public void FindOrExpandTo_returns_the_node_for_the_target_path()
    {
        var target = Path.GetTempPath();

        var found = TempDriveRoot().FindOrExpandTo(target);

        Assert.NotNull(found);
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(target)),
            Path.TrimEndingDirectorySeparator(found!.Path),
            ignoreCase: true);
    }

    [Fact]
    public void FindOrExpandTo_selects_the_target_so_the_tree_highlights_it()
    {
        var found = TempDriveRoot().FindOrExpandTo(Path.GetTempPath());

        Assert.True(found!.IsSelected);
    }

    [Fact]
    public void FindOrExpandTo_expands_every_ancestor_so_the_target_is_reachable()
    {
        var root = TempDriveRoot();

        root.FindOrExpandTo(Path.GetTempPath());

        Assert.True(root.IsExpanded);
    }

    /// <summary>
    /// The target itself is left collapsed: expanding it would read its subfolders for no reason,
    /// and Explorer does not expand the folder you navigate to either.
    /// </summary>
    [Fact]
    public void FindOrExpandTo_leaves_the_target_itself_collapsed()
    {
        var found = TempDriveRoot().FindOrExpandTo(Path.GetTempPath());

        Assert.False(found!.IsExpanded);
    }

    [Fact]
    public void FindOrExpandTo_returns_null_for_a_path_on_another_root()
    {
        var node = new FolderTreeNodeViewModel(@"Q:\", @"Q:\");

        Assert.Null(node.FindOrExpandTo(Path.GetTempPath()));
    }

    [Fact]
    public void FindOrExpandTo_returns_null_rather_than_throwing_on_a_malformed_path() =>
        Assert.Null(TempDriveRoot().FindOrExpandTo("|not<a>path|"));

    [Fact]
    public void CreateDriveRoots_lists_at_least_the_drive_the_temp_folder_lives_on()
    {
        var expected = Path.GetPathRoot(Path.GetTempPath())!;

        var roots = FolderTreeNodeViewModel.CreateDriveRoots();

        Assert.Contains(roots, r => string.Equals(r.Path, expected, StringComparison.OrdinalIgnoreCase));
    }
}
