namespace MIPLabelCustomCheck.Internal;

internal sealed record LabelReadResult(
    LabelStatus Status,
    IReadOnlyList<Guid> LabelIds,
    ProtectionType Protection,
    IReadOnlyList<LabelDetail> Details,
    IReadOnlyList<LabelSource> Sources,
    string ProtectionDetail)
{
    internal static LabelReadResult Labeled(
        IEnumerable<Guid> ids,
        ProtectionType protection = ProtectionType.None,
        IReadOnlyList<LabelDetail>? details = null,
        IReadOnlyList<LabelSource>? sources = null,
        string protectionDetail = "") =>
        new(
            LabelStatus.Labeled,
            ids.Distinct().ToArray(),
            protection,
            details ?? [],
            sources ?? [],
            protectionDetail);

    internal static LabelReadResult Unlabeled() =>
        new(LabelStatus.Unlabeled, [], ProtectionType.None, [], [], "");

    internal static LabelReadResult ProtectedUnknown(
        ProtectionType protection,
        string protectionDetail = "") =>
        new(LabelStatus.ProtectedLabelUnknown, [], protection, [], [], protectionDetail);

    internal static LabelReadResult Unsupported() =>
        new(LabelStatus.Unsupported, [], ProtectionType.None, [], [], "");

    internal static LabelReadResult Invalid() =>
        new(LabelStatus.InvalidFile, [], ProtectionType.None, [], [], "");

    internal LabelCheckResult ToPublic() =>
        new(Status, LabelIds, Protection, Details, Sources, ProtectionDetail);
}
