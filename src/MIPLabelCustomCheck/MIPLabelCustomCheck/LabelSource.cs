namespace MIPLabelCustomCheck;

/// <summary>
/// Where a reported sensitivity-label GUID was read from.
/// </summary>
/// <remarks>
/// Entries in <see cref="LabelCheckResult.Sources"/> run parallel to
/// <see cref="LabelCheckResult.LabelIds"/>: <c>Sources[i]</c> is where
/// <c>LabelIds[i]</c> was first seen. When the same label appears in two
/// locations (for example, a PDF's Info dictionary and its XMP stream),
/// the first-seen location wins, matching the <c>LabelIds</c> order.
/// File metadata is unauthenticated input — a source tells you where the
/// bytes were found, not that the label is genuine. Always authorize
/// against <c>LabelIds</c> (and <c>SiteId</c>), never against display
/// names or sources alone.
/// </remarks>
public enum LabelSource
{
    /// <summary>Structured LabelInfo XML (OOXML <c>LabelInfo.xml</c>, CFB LabelInfo stream).</summary>
    LabelInfo,

    /// <summary>Legacy <c>MSIP_Label_{guid}_Enabled</c> custom properties
    /// (OOXML <c>docProps/custom.xml</c>, CFB DocumentSummaryInformation).</summary>
    LegacyProperty,

    /// <summary>XMP packet (PNG <c>iTXt</c> chunk, PDF <c>/Metadata</c> stream).</summary>
    Xmp,

    /// <summary>PDF Info dictionary string values.</summary>
    PdfInfoDictionary,

    /// <summary>Outlook MSG MAPI string named properties.</summary>
    MsgNamedProperty,

    /// <summary>RFC 5322 email headers.</summary>
    EmailHeader
}
