using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIPLabelCustomCheck.Explorer.ViewModels;

/// <summary>
/// Minimal <see cref="INotifyPropertyChanged"/> base, hand-written to keep the app free of
/// NuGet dependencies.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }

    protected void Raise(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
