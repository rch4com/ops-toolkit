using System.Globalization;
using System.Windows.Data;

namespace MIPLabelCustomCheck.Explorer.Converters;

/// <summary>
/// Formats an elapsed time as milliseconds to two decimals. Integer milliseconds would render
/// almost every warmed check as "0 ms" — warmed checks run in roughly 0.1-0.7 ms.
/// </summary>
public sealed class ElapsedToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TimeSpan elapsed
            ? $"{elapsed.TotalMilliseconds.ToString("F2", culture)} ms"
            : "-";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
