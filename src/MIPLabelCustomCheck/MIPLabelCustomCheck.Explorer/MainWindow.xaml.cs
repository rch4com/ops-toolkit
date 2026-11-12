using System.Windows;
using System.Windows.Controls;
using MIPLabelCustomCheck.Explorer.ViewModels;

namespace MIPLabelCustomCheck.Explorer;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>
    /// Forwards the tree's selection to the view model. <see cref="TreeView.SelectedItem"/> is
    /// read-only, so it cannot be bound two-way like the list's selection.
    /// </summary>
    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is FolderTreeNodeViewModel node)
        {
            viewModel.SelectedNode = node;
        }
    }

    /// <summary>
    /// Scrolls a newly selected node into view. Typing a path expands the tree down to a node that
    /// is usually well below the visible area, so without this the tree looks like nothing
    /// happened. Deliberately does not mark the event handled — the tree needs it to keep tracking
    /// its own selection.
    /// </summary>
    private void OnTreeItemSelected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item)
        {
            item.BringIntoView();
        }
    }

    /// <summary>Opens the double-clicked file in its associated program.</summary>
    /// <remarks>
    /// Hung off the row rather than the whole list so that double-clicking a column header or the
    /// empty space below the rows does not open whatever happens to be selected.
    /// </remarks>
    private void OnFileDoubleClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.OpenSelectedFile();
        }
    }
}
