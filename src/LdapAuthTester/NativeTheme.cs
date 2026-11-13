using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace LdapAuthTester;

/// <summary>Maps the frozen Nocturne palette to native controls without changing their layout.</summary>
internal static class NativeTheme
{
    private static readonly ConditionalWeakTable<Form, object> SubscribedForms = new();
    private static readonly ConditionalWeakTable<Button, object> SubscribedButtons = new();

    internal static void Apply(Control root)
    {
        bool highContrast = SystemInformation.HighContrast;
        bool dark = !highContrast && IsDark();
        Color background = highContrast ? SystemColors.Control : ColorTranslator.FromHtml(dark ? "#161826" : "#EEF0F9");
        Color surface = highContrast ? SystemColors.Window : ColorTranslator.FromHtml(dark ? "#232532" : "#F8F9FE");
        Color text = highContrast ? SystemColors.ControlText : ColorTranslator.FromHtml(dark ? "#E9E9ED" : "#292B31");
        Color accent = highContrast ? SystemColors.Highlight : ColorTranslator.FromHtml(dark ? "#9184D9" : "#796CBF");
        Color accentText = highContrast ? SystemColors.ControlText : ColorTranslator.FromHtml(dark ? "#D2CEFD" : "#5D5294");
        ApplyControls(root);

        if (root is Form form && !SubscribedForms.TryGetValue(form, out _))
        {
            SubscribedForms.Add(form, new object());
            UserPreferenceChangedEventHandler changed = (_, _) =>
            {
                if (!form.IsDisposed && form.IsHandleCreated)
                {
                    form.BeginInvoke(() =>
                    {
                        if (!form.IsDisposed)
                        {
                            Apply(form);
                        }
                    });
                }
            };
            SystemEvents.UserPreferenceChanged += changed;
            form.Disposed += (_, _) => SystemEvents.UserPreferenceChanged -= changed;
        }

        void ApplyControls(Control control)
        {
            control.BackColor = control is TextBoxBase or ComboBox or ListView ? surface : background;
            control.ForeColor = highContrast && control is TextBoxBase or ComboBox or ListView ? SystemColors.WindowText : text;
            if (control is Button button)
            {
                bool useSystem = highContrast || !button.Enabled;
                button.FlatStyle = useSystem ? FlatStyle.System : FlatStyle.Flat;
                button.UseVisualStyleBackColor = useSystem;
                button.FlatAppearance.BorderColor = accent;
                button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml(dark ? "#2B2741" : "#E7E5FE");
                button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml(dark ? "#423A6A" : "#D2CEFD");
                button.ForeColor = accentText;
                if (!SubscribedButtons.TryGetValue(button, out _))
                {
                    SubscribedButtons.Add(button, new object());
                    button.EnabledChanged += (_, _) => Apply(button);
                }
            }

            foreach (Control child in control.Controls)
            {
                ApplyControls(child);
            }
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
