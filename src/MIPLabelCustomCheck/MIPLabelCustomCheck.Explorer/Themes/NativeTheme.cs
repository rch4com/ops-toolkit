using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MIPLabelCustomCheck.Explorer.Themes;

/// <summary>Applies Nocturne resources while preserving the Windows high contrast palette.</summary>
internal static class NativeTheme
{
    internal static void Start()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemPropertyChanged;
    }

    internal static void Stop()
    {
        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemPropertyChanged;
    }

    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Refresh();

    private static void OnSystemPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private static void Refresh()
    {
        var app = Application.Current;
        if (app is not null && !app.Dispatcher.HasShutdownStarted)
        {
            app.Dispatcher.InvokeAsync(Apply);
        }
    }

    private static void Apply()
    {
        bool highContrast = SystemParameters.HighContrast;
        bool dark = !highContrast && IsDark();
        var resources = Application.Current.Resources;
        Set("CanvasBrush", "#EEF0F9", "#161826", SystemColors.ControlColor);
        Set("SurfaceBrush", "#F8F9FE", "#232532", SystemColors.WindowColor);
        Set("SubtleBrush", "#E4E7F5", "#292B31", SystemColors.ControlColor);
        Set("TextBrush", "#292B31", "#E9E9ED", SystemColors.WindowTextColor);
        Set("MutedBrush", "#595D6C", "#B2B6CA", SystemColors.WindowTextColor);
        Set("BorderBrush", "#75798C", "#75798C", SystemColors.WindowTextColor);
        Set("AccentBrush", "#796CBF", "#9184D9", SystemColors.HighlightColor);
        Set("AccentTextBrush", "#5D5294", "#D2CEFD", SystemColors.WindowTextColor);
        Set("SelectionBrush", "#E7E5FE", "#2B2741", SystemColors.HighlightColor);
        Set("SelectionTextBrush", "#292B31", "#E9E9ED", SystemColors.HighlightTextColor);
        Set("PressedBrush", "#D2CEFD", "#423A6A", SystemColors.HighlightColor);
        Set("ErrorBrush", "#A1263D", "#FFAFBA", SystemColors.WindowTextColor);
        Set("ErrorSurfaceBrush", "#FBE8ED", "#482C37", SystemColors.WindowColor);

        void Set(string key, string light, string darkColor, Color system)
        {
            var brush = new SolidColorBrush(highContrast ? system : (Color)ColorConverter.ConvertFromString(dark ? darkColor : light));
            brush.Freeze();
            resources[key] = brush;
        }
    }

    private static bool IsDark()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
