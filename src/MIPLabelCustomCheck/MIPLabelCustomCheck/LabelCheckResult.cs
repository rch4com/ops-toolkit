using System.Collections.ObjectModel;

namespace MIPLabelCustomCheck;

/// <summary>
/// The outcome of inspecting a file for Microsoft Purview sensitivity-label metadata.
/// </summary>
public enum LabelStatus
{
    /// <summary>At least one enabled sensitivity label GUID was found; see
    /// <see cref="LabelCheckResult.LabelIds"/>.</summary>
    Labeled,

    /// <summary>The file's format is supported and was read successfully, but it carries no
    /// enabled sensitivity label.</summary>
    Unlabeled,

    /// <summary>The file is protected (encrypted or rights-managed) in a way that prevents this
    /// library from reading its label metadata, so whether it is labeled cannot be determined.
    /// See <see cref="LabelCheckResult.Protection"/> for the kind of protection detected.</summary>
    ProtectedLabelUnknown,

    /// <summary>The file's format, or the specific feature of it encountered, is not one this
    /// library reads label metadata from.</summary>
    Unsupported,

    /// <summary>The file's content does not conform to its format's expected structure and could
    /// not be parsed.</summary>
    InvalidFile
}

/// <summary>
/// The kind of protection detected on a file, independent of whether a label GUID could be
/// read from it.
/// </summary>
public enum ProtectionType
{
    /// <summary>No protection was detected.</summary>
    None,

    /// <summary>The file uses the format's own native protection mechanism (for example,
    /// Office IRM/DataSpaces encryption, or a PDF <c>/Encrypt</c> entry).</summary>
    Native,

    /// <summary>The file is a generic <c>.pfile</c>-protected file. Its protected payload is
    /// opaque, so it may carry label-based protection, general Rights Management protection, or
    /// both; this library does not distinguish between them.</summary>
    GenericPFile
}

/// <summary>
/// The result of inspecting a single file or stream for Microsoft Purview sensitivity-label
/// metadata via <see cref="MipLabelChecker"/>.
/// </summary>
public sealed record LabelCheckResult
{
    private readonly ReadOnlyCollection<Guid> _labelIds;
    private readonly ReadOnlyCollection<LabelDetail> _details;
    private readonly ReadOnlyCollection<LabelSource> _sources;

    internal LabelCheckResult(
        LabelStatus status,
        IEnumerable<Guid> labelIds,
        ProtectionType protection,
        IEnumerable<LabelDetail>? details = null,
        IEnumerable<LabelSource>? sources = null,
        string protectionDetail = "")
    {
        Status = status;
        _labelIds = Array.AsReadOnly(labelIds.Distinct().ToArray());
        _details = Array.AsReadOnly(details?.ToArray() ?? []);
        _sources = Array.AsReadOnly(sources?.ToArray() ?? []);
        Protection = protection;
        ProtectionDetail = protection == ProtectionType.None ? string.Empty : protectionDetail;
    }

    /// <summary>The overall outcome of the inspection.</summary>
    public LabelStatus Status { get; }

    /// <summary>
    /// The distinct enabled sensitivity label GUIDs found in the file, in the order first
    /// encountered. Empty unless <see cref="Status"/> is <see cref="LabelStatus.Labeled"/>.
    /// </summary>
    public IReadOnlyList<Guid> LabelIds => _labelIds;

    /// <summary>The kind of protection detected on the file, independent of <see cref="Status"/>.</summary>
    public ProtectionType Protection { get; }

    /// <summary>
    /// Format-specific evidence for <see cref="Protection"/>, such as a PDF <c>/Encrypt</c>
    /// filter or a legacy Office CFB protection signal. Empty when no detail was available.
    /// </summary>
    public string ProtectionDetail { get; }

    /// <summary>
    /// Each label's supplementary metadata — tenant, name, method, and so on — in the same order
    /// as <see cref="LabelIds"/>.
    /// </summary>
    /// <remarks>
    /// Empty unless the call passed <see cref="LabelCheckOptions"/> with
    /// <see cref="LabelCheckOptions.IncludeDetails"/> set. An empty list therefore means "details
    /// were not asked for", not "the file has none — check <see cref="LabelIds"/> to tell the two
    /// apart.
    /// </remarks>
    public IReadOnlyList<LabelDetail> Details => _details;

    /// <summary>
    /// Where each reported label GUID was read from, parallel to <see cref="LabelIds"/>:
    /// <c>Sources[i]</c> is where <c>LabelIds[i]</c> was first seen. Empty unless
    /// <see cref="Status"/> is <see cref="LabelStatus.Labeled"/>.
    /// </summary>
    public IReadOnlyList<LabelSource> Sources => _sources;

    /// <summary>Convenience shortcut for <c><see cref="Status"/> == <see cref="LabelStatus.Labeled"/></c>.</summary>
    public bool IsLabeled => Status == LabelStatus.Labeled;

    /// <summary>
    /// The single label GUID when exactly one was found, or <see langword="null"/> when zero or
    /// more than one were found. Prefer <see cref="LabelIds"/> when a file may carry multiple
    /// labels.
    /// </summary>
    public Guid? LabelId => _labelIds.Count == 1 ? _labelIds[0] : null;

    /// <inheritdoc/>
    public bool Equals(LabelCheckResult? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        Status == other.Status &&
        Protection == other.Protection &&
        ProtectionDetail == other.ProtectionDetail &&
        _labelIds.SequenceEqual(other._labelIds) &&
        _details.SequenceEqual(other._details) &&
        _sources.SequenceEqual(other._sources);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        hash.Add(Protection);
        hash.Add(ProtectionDetail);

        foreach (var labelId in _labelIds)
        {
            hash.Add(labelId);
        }

        foreach (var detail in _details)
        {
            hash.Add(detail);
        }

        foreach (var source in _sources)
        {
            hash.Add(source);
        }

        return hash.ToHashCode();
    }
}
