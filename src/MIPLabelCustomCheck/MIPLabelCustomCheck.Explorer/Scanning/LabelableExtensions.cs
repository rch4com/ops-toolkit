using System.IO;

namespace MIPLabelCustomCheck.Explorer.Scanning;

/// <summary>
/// The file name extensions Microsoft Purview Information Protection can apply a sensitivity
/// label to, plus the generic-protection <c>.pfile</c> container. Used as the default set of
/// extensions the app checks, so a scan spends its time on files that could plausibly carry a
/// label instead of on every <c>.dll</c> in the folder.
/// </summary>
/// <remarks>
/// <para>
/// Sourced from "Supported file types" in Microsoft's documentation for the Purview Information
/// Protection client: https://learn.microsoft.com/en-us/purview/information-protection-client
/// </para>
/// <para>
/// This is the set Purview can <em>label</em>, plus Outlook email containers (<c>.msg</c>, <c>.eml</c>)
/// and <c>.pfile</c>, which is wider than the set this library can <em>read</em> a label from.
/// The library detects format by content signature and reads OOXML, legacy Office CFB, Outlook
/// <c>.msg</c>, <c>.eml</c>, PDF, PNG, JPEG, TIFF, and <c>.pfile</c>; the remaining extensions
/// here — <c>.psd</c>, <c>.dng</c>, <c>.txt</c>, and so on — report as unsupported. That gap is
/// deliberate and worth showing: it is the honest answer to "Purview labels this, can this
/// library read it?"
/// </para>
/// <para>
/// The natively-encrypted output extensions (<c>.ppng</c>, <c>.pjpg</c>, <c>.ptxt</c>, …) are not
/// included: those are what a labelled file <em>becomes</em>, not an extension Purview labels.
/// Add them through the app's extension box if you need to hunt for them.
/// </para>
/// </remarks>
public static class LabelableExtensions
{
    /// <summary>The default extension set: everything Purview can label, plus <c>.msg</c>, <c>.eml</c>, and <c>.pfile</c>.</summary>
    public static IReadOnlySet<string> Default { get; } = new HashSet<string>(
        [
            // Microsoft Office — 97-2003 and Open XML formats.
            ".doc", ".docm", ".docx", ".dot", ".dotm", ".dotx",
            ".xls", ".xlsb", ".xlsm", ".xlsx", ".xlt", ".xltm", ".xltx", ".xla", ".xlam",
            ".ppt", ".pptm", ".pptx", ".pot", ".potm", ".potx", ".pps", ".ppsm", ".ppsx",
            ".vdw", ".vsd", ".vsdm", ".vsdx", ".vss", ".vssm", ".vssx", ".vst", ".vstm", ".vstx",

            // Outlook / Email formats.
            ".msg", ".eml",

            // Adobe Portable Document Format.
            ".pdf",

            // Microsoft Project, Publisher, XPS.
            ".mpp", ".mpt", ".pub", ".xps", ".oxps",

            // Images.
            ".jpg", ".jpe", ".jpeg", ".jif", ".jfif", ".jfi", ".png", ".tif", ".tiff",
            ".bmp", ".gif", ".jt",

            // Autodesk Design Review, Adobe Photoshop, Digital Negative.
            ".dwfx", ".psd", ".dng",

            // Text formats Purview encrypts natively.
            ".txt", ".xml",

            // Generic protection container. Every other file type Purview protects becomes one of
            // these, so it is the one extension that must never be dropped from the set.
            ".pfile"
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="filePath"/>'s extension is in <paramref name="extensions"/>.</summary>
    public static bool Includes(IReadOnlySet<string> extensions, string filePath)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(filePath);

        var extension = Path.GetExtension(filePath);
        return extension.Length > 0 && extensions.Contains(extension);
    }

    /// <summary>
    /// Parses a user-typed list such as <c>".docx, pdf; .pfile"</c>. Separators are commas,
    /// semicolons, and whitespace; a leading dot is added when missing; blanks are dropped.
    /// </summary>
    public static IReadOnlySet<string> Parse(string? text)
    {
        var parsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
        {
            return parsed;
        }

        foreach (var token in text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = token.Trim();
            if (trimmed.Length == 0 || trimmed == ".")
            {
                continue;
            }

            parsed.Add(trimmed.StartsWith('.') ? trimmed : $".{trimmed}");
        }

        return parsed;
    }

    /// <summary>Renders an extension set back into the comma-separated form <see cref="Parse"/> reads.</summary>
    public static string Format(IEnumerable<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        return string.Join(", ", extensions.Order(StringComparer.OrdinalIgnoreCase));
    }
}
