using System.Globalization;

namespace MIPLabelCustomCheck.Explorer.ViewModels;

/// <summary>
/// One label's supplementary metadata, formatted for the detail pane.
/// </summary>
/// <remarks>
/// Every field is rendered here rather than bound straight through, for two reasons: the library's
/// values are nullable and would bind as blanks that read like bugs, and <see cref="LabelMethod"/>
/// would print its raw enum name in a UI that is otherwise Korean.
/// </remarks>
public sealed class LabelDetailViewModel(LabelDetail detail)
{
    private const string Absent = "(없음)";

    public Guid Id { get; } = detail.Id;

    public string IdText { get; } = detail.Id.ToString();

    public string SiteIdText { get; } = detail.SiteId?.ToString() ?? Absent;

    public string NameText { get; } = detail.Name ?? Absent;

    public string MethodText { get; } = detail.Method switch
    {
        LabelMethod.Privileged => "Privileged (명시적으로 적용)",
        LabelMethod.Standard => "Standard (정책에 의해 적용)",
        _ => Absent
    };

    public string ContentBitsText { get; } =
        detail.ContentBits?.ToString(CultureInfo.InvariantCulture) ?? Absent;

    public string ActionIdText { get; } = detail.ActionId?.ToString() ?? Absent;

    public string SetDateText { get; } =
        detail.SetDate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
        ?? Absent;
}
