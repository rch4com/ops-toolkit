namespace MIPLabelCustomCheck.Explorer.ViewModels;

/// <summary>
/// Where a file sits in this app's check lifecycle, independent of the library's own
/// <see cref="LabelStatus"/>.
/// </summary>
public enum CheckState
{
    /// <summary>Queued but not yet checked.</summary>
    Pending,

    /// <summary>
    /// Its extension is outside the configured set, so the scan did not check it. Says nothing
    /// about the file — clicking it checks it anyway, which is how a file with a lying extension
    /// gets caught.
    /// </summary>
    Skipped,

    /// <summary>Its check is running now.</summary>
    Running,

    /// <summary>Checked; see the file's <see cref="LabelCheckResult"/>.</summary>
    Completed,

    /// <summary>The scan was cancelled before this file's check started. Not the same as unlabeled.</summary>
    Canceled,

    /// <summary>The check threw — the file is locked, unreadable, or something unexpected happened.</summary>
    Failed
}
