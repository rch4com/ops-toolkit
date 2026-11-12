using System.Xml;
using MIPLabelCustomCheck.Internal.CompoundFile;

namespace MIPLabelCustomCheck.Internal;

/// <summary>
/// A minimal view over a compound (OLE/CFB) file's storages and streams, so
/// <see cref="CompoundFileLabelReader"/> can be tested against synthetic layouts without
/// needing to build a real compound binary file.
/// </summary>
internal interface ICompoundFileView : IDisposable
{
    bool Contains(string path);

    bool TryReadStream(string path, out byte[] bytes);
}

/// <summary>
/// Reads Microsoft Purview sensitivity labels from legacy (pre-OOXML) Office binary files and
/// from encrypted/IRM-protected Office compound files. Precedence: a valid LabelInfo stream
/// under DataSpaces/TransformInfo wins over legacy custom document properties; any tenant
/// (SiteId) that appears in LabelInfo, active or removed, suppresses that tenant's legacy
/// custom property. The presence of a DataSpaces area, an EncryptedPackage stream, or an IRM
/// transform storage is treated as native (non-MIP-specific) protection.
/// </summary>
internal static class CompoundFileLabelReader
{
    private const string DocumentSummaryInformationPath = "DocumentSummaryInformation";
    private const string DataSpacesPath = "DataSpaces";
    private const string LabelInfoPath = "DataSpaces/TransformInfo/LabelInfo";
    private const string EncryptedPackagePath = "EncryptedPackage";
    // Nested under DataSpaces, so DataSpacesPath alone already covers files carrying this
    // storage; kept as its own explicit check for defense-in-depth, so it stays a direct
    // signal for IRM protection even if the DataSpaces storage-name check above changes.
    private const string IrmTransformPath = "DataSpaces/TransformInfo/Microsoft.Metadata.IRMProtector";
    private const string LegacyPropertyPrefix = "MSIP_Label_";
    private const string SiteIdPropertySuffix = "_SiteId";
    private const int MaxLabelInfoBytes = 1024 * 1024;

    internal static LabelReadResult Read(Stream stream, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new CompoundFileReader(stream, leaveOpen: true);
            using var view = new CompoundFileReaderView(reader);
            return ReadCore(view, includeDetails);
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException)
        {
            return LabelReadResult.Invalid();
        }
    }

    internal static LabelReadResult Read(ICompoundFileView view, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(view);

        try
        {
            return ReadCore(view, includeDetails);
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException)
        {
            return LabelReadResult.Invalid();
        }
    }

    private static LabelReadResult ReadCore(ICompoundFileView view, bool includeDetails)
    {
        var protectionDetail = ReadProtectionDetail(view);
        var protection = protectionDetail.Length == 0 ? ProtectionType.None : ProtectionType.Native;

        var labelInfoRecords = ReadLabelInfoRecords(view);

        var ownedTenants = labelInfoRecords
            .Where(record => record.SiteId.HasValue)
            .Select(record => record.SiteId!.Value)
            .ToHashSet();

        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var sources = new List<LabelSource>();

        // 부가 정보는 채택된 레이블만, 채택된 순서대로 모은다 (OpenXmlLabelReader와 같은 규칙).
        var details = includeDetails ? new List<LabelDetail>() : null;

        foreach (var record in labelInfoRecords)
        {
            if (record.Enabled && !record.Removed && seenLabelIds.Add(record.LabelId))
            {
                labelIds.Add(record.LabelId);
                sources.Add(LabelSource.LabelInfo);
                details?.Add(record.ToDetail());
            }
        }

        var legacy = ReadLegacyLabels(view, includeDetails);
        foreach (var (labelId, siteId) in legacy.Labels)
        {
            if (siteId.HasValue && ownedTenants.Contains(siteId.Value))
            {
                continue;
            }

            if (seenLabelIds.Add(labelId))
            {
                labelIds.Add(labelId);
                sources.Add(LabelSource.LegacyProperty);
                if (details is not null &&
                    legacy.Details.FirstOrDefault(d => d.Id == labelId) is { } detail)
                {
                    details.Add(detail);
                }
            }
        }

        if (labelIds.Count > 0)
        {
            return LabelReadResult.Labeled(labelIds, protection, details, sources, protectionDetail);
        }

        return protection == ProtectionType.Native
            ? LabelReadResult.ProtectedUnknown(ProtectionType.Native, protectionDetail)
            : LabelReadResult.Unlabeled();
    }

    private static string ReadProtectionDetail(ICompoundFileView view)
    {
        if (view.Contains(IrmTransformPath))
        {
            return "IRM transform";
        }

        if (view.Contains(EncryptedPackagePath))
        {
            return "EncryptedPackage";
        }

        return view.Contains(DataSpacesPath) ? "DataSpaces" : string.Empty;
    }

    private static IReadOnlyList<LabelInfoRecord> ReadLabelInfoRecords(ICompoundFileView view)
    {
        if (!view.TryReadStream(LabelInfoPath, out var bytes))
        {
            return [];
        }

        if (bytes.Length > MaxLabelInfoBytes)
        {
            throw new InvalidDataException("The compound file LabelInfo stream exceeds the byte limit.");
        }

        using var stream = new MemoryStream(bytes, writable: false);
        var result = LabelInfoXmlReader.Read(stream, MaxLabelInfoBytes);
        if (result.InvalidMetadata)
        {
            throw new InvalidDataException("The compound file LabelInfo metadata is invalid.");
        }

        return result.Records;
    }

    private sealed record LegacyLabels(
        IReadOnlyList<(Guid LabelId, Guid? SiteId)> Labels,
        IReadOnlyList<LabelDetail> Details)
    {
        internal static LegacyLabels None { get; } = new([], []);
    }

    private static LegacyLabels ReadLegacyLabels(ICompoundFileView view, bool includeDetails)
    {
        if (!view.TryReadStream(DocumentSummaryInformationPath, out var bytes))
        {
            return LegacyLabels.None;
        }

        if (bytes.Length > CompoundFileReader.MaxMetadataStreamBytes)
        {
            throw new InvalidDataException(
                "The compound file DocumentSummaryInformation stream exceeds the byte limit.");
        }

        var propertiesResult = OlePropertySetReader.ReadUserDefinedProperties(bytes);
        if (propertiesResult.Status != OlePropertySetStatus.Success)
        {
            return LegacyLabels.None;
        }

        var parsed = LabelPropertyParser.Parse(propertiesResult.Properties, includeDetails);
        if (parsed.InvalidMetadata)
        {
            throw new InvalidDataException("The compound file custom label properties are invalid.");
        }

        if (parsed.LabelIds.Count == 0)
        {
            return LegacyLabels.None;
        }

        var activeLabelIds = new HashSet<Guid>(parsed.LabelIds);
        var siteIdsByLabelId = new Dictionary<Guid, Guid>();

        foreach (var property in propertiesResult.Properties)
        {
            if (!TryParseSiteIdKey(property.Key, out var labelId) || !activeLabelIds.Contains(labelId))
            {
                continue;
            }

            if (!Guid.TryParse(property.Value, out var siteId))
            {
                throw new InvalidDataException("The compound file custom label site identifier is invalid.");
            }

            siteIdsByLabelId[labelId] = siteId;
        }

        var labels = parsed.LabelIds
            .Select(labelId => (
                labelId,
                siteIdsByLabelId.TryGetValue(labelId, out var siteId) ? (Guid?)siteId : null))
            .ToArray();

        return new LegacyLabels(labels, parsed.Details);
    }

    private static bool TryParseSiteIdKey(string propertyName, out Guid labelId)
    {
        labelId = default;
        if (!propertyName.StartsWith(LegacyPropertyPrefix, StringComparison.Ordinal) ||
            !propertyName.EndsWith(SiteIdPropertySuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var guidText = propertyName[LegacyPropertyPrefix.Length..^SiteIdPropertySuffix.Length];
        return Guid.TryParse(guidText, out labelId);
    }

    private sealed class CompoundFileReaderView(CompoundFileReader reader) : ICompoundFileView
    {
        public bool Contains(string path) => reader.Contains(path);

        public bool TryReadStream(string path, out byte[] bytes) => reader.TryReadStream(path, out bytes);

        public void Dispose() => reader.Dispose();
    }
}
