using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MIPLabelCustomCheck.Explorer.ViewModels;

namespace MIPLabelCustomCheck.Explorer.Converters;

/// <summary>
/// Colours a row by its check state so labelled, protected, and failed files read apart at a glance.
/// </summary>
public sealed class CheckStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            CheckState.Completed => Brushes.Black,
            CheckState.Failed => Brushes.Firebrick,
            CheckState.Canceled => Brushes.DarkGray,
            CheckState.Skipped => Brushes.Silver,
            CheckState.Running => Brushes.SteelBlue,
            _ => Brushes.Gray
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
