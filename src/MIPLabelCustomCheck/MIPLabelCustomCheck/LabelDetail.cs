namespace MIPLabelCustomCheck;

/// <summary>How a Microsoft Purview sensitivity label came to be applied to a file.</summary>
public enum LabelMethod
{
    /// <summary>The file's metadata did not say, or said something this library does not recognize.</summary>
    Unspecified,

    /// <summary>The label was applied automatically or recommended by policy.</summary>
    Standard,

    /// <summary>The label was applied explicitly — by a user, an administrator, or a script.</summary>
    Privileged
}

/// <summary>
/// The supplementary metadata a Microsoft Purview sensitivity label carries alongside its GUID.
/// </summary>
/// <remarks>
/// <para>
/// Only populated when <see cref="LabelCheckOptions.IncludeDetails"/> is set; see
/// <see cref="LabelCheckResult.Details"/>.
/// </para>
/// <para>
/// Every property except <see cref="Id"/> is optional, because which fields a file carries depends
/// on the client that wrote them and on the format. Of this repository's real samples, the PNG
/// carries all six, the legacy Office files carry all six, the PDF carries only
/// <see cref="SiteId"/>, <see cref="Method"/>, and <see cref="ContentBits"/>, and the OOXML files
/// carry those three in their <c>LabelInfo.xml</c>. A null means "the file did not say", never
/// "the value is empty".
/// </para>
/// </remarks>
public sealed record LabelDetail
{
    /// <summary>The label's GUID. Always present — it is what identifies the label.</summary>
    public required Guid Id { get; init; }

    /// <summary>The tenant the label belongs to, from <c>SiteId</c>.</summary>
    public Guid? SiteId { get; init; }

    /// <summary>
    /// The label's display name at the time it was applied, from <c>Name</c>. A snapshot, not a
    /// live lookup: if the label has since been renamed in the tenant's policy, this still shows
    /// the old name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>How the label was applied, from <c>Method</c>.</summary>
    public LabelMethod Method { get; init; }

    /// <summary>
    /// The label's content-marking bits, from <c>ContentBits</c>: a flags value describing which
    /// visual markings (header, footer, watermark) the label applies.
    /// </summary>
    public int? ContentBits { get; init; }

    /// <summary>The labelling operation's identifier, from <c>ActionId</c>. Changes each time the file is relabelled.</summary>
    public Guid? ActionId { get; init; }

    /// <summary>When the label was applied, from <c>SetDate</c>.</summary>
    public DateTimeOffset? SetDate { get; init; }
}
