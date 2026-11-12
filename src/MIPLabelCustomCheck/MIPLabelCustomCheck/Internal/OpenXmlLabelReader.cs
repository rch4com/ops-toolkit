using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace MIPLabelCustomCheck.Internal;

internal static class OpenXmlLabelReader
{
    private const int MaximumMetadataBytes = 1024 * 1024;
    internal const int MaxStructuralEntryCount = 64;
    internal const long MaxAggregateStructuralBytes = 4L * 1024 * 1024;
    private const string ContentTypesEntryName = "[Content_Types].xml";
    private const string RelationshipsEntryName = "_rels/.rels";
    private const string CustomPropertiesEntryName = "docProps/custom.xml";
    private const string ClassificationRelationshipType =
        "http://schemas.microsoft.com/office/2020/02/relationships/classificationlabels";
    private const string LegacyPropertyPrefix = "MSIP_Label_";
    private const string SiteIdPropertySuffix = "_SiteId";

    private static readonly XNamespace ContentTypesNamespace =
        "http://schemas.openxmlformats.org/package/2006/content-types";

    private static readonly XNamespace RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly XNamespace CustomPropertiesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

    private static readonly HashSet<string> OfficeMainContentTypes =
        new(StringComparer.Ordinal)
        {
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml",
            "application/vnd.ms-word.document.macroEnabled.main+xml",
            "application/vnd.ms-word.template.macroEnabledTemplate.main+xml",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml",
            "application/vnd.ms-excel.sheet.macroEnabled.main+xml",
            "application/vnd.ms-excel.template.macroEnabled.main+xml",
            "application/vnd.ms-excel.addin.macroEnabled.main+xml",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
            "application/vnd.openxmlformats-officedocument.presentationml.slideshow.main+xml",
            "application/vnd.openxmlformats-officedocument.presentationml.template.main+xml",
            "application/vnd.ms-powerpoint.presentation.macroEnabled.main+xml",
            "application/vnd.ms-powerpoint.slideshow.macroEnabled.main+xml",
            "application/vnd.ms-powerpoint.template.macroEnabled.main+xml",
            "application/vnd.ms-powerpoint.addin.macroEnabled.main+xml"
        };

    private static readonly HashSet<string> OfficeDocumentRelationshipTypes =
        new(StringComparer.Ordinal)
        {
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
            "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"
        };

    internal static LabelReadResult Read(Stream stream, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var parts = CreatePartIndex(archive);
            var contentTypeEntries = parts.GetEquivalentEntries(ContentTypesEntryName);
            var relationshipEntries = parts.GetEquivalentEntries(RelationshipsEntryName);
            if (!IsWithinStructuralBudgets(contentTypeEntries, relationshipEntries))
            {
                return LabelReadResult.Invalid();
            }

            var contentTypes = InspectContentTypes(contentTypeEntries);
            if (parts.HasEquivalentDuplicates && contentTypes.Evidence.HasOfficeEvidence)
            {
                return LabelReadResult.Invalid();
            }

            var relationships = InspectRelationships(relationshipEntries);
            var contentTypeEvidence = contentTypes.Evidence;
            var relationshipEvidence = relationships.Evidence;

            if (!contentTypeEvidence.HasOfficeEvidence &&
                !relationshipEvidence.HasOfficeEvidence)
            {
                return LabelReadResult.Unsupported();
            }

            if (parts.HasEquivalentDuplicates ||
                contentTypes.HasInvalidMetadata ||
                relationships.HasInvalidMetadata ||
                !contentTypeEvidence.HasValidStructure ||
                !relationshipEvidence.HasValidStructure ||
                !contentTypeEvidence.HasValidCandidate ||
                !relationshipEvidence.HasValidCandidate ||
                !string.Equals(
                    contentTypeEvidence.MainPartName,
                    relationshipEvidence.MainPartName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LabelReadResult.Invalid();
            }

            _ = GetPart(
                parts,
                contentTypeEvidence.MainPartName!,
                required: true);

            return ReadOfficePackage(parts, relationshipEvidence.Relationships, includeDetails);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or XmlException or UriFormatException)
        {
            return LabelReadResult.Invalid();
        }
    }

    private static LabelReadResult ReadOfficePackage(
        ArchivePartIndex parts,
        IReadOnlyList<XElement> relationships,
        bool includeDetails)
    {
        var classificationRelationships = relationships
            .Where(
                relationship =>
                    string.Equals(
                        relationship.Attribute("Type")?.Value,
                        ClassificationRelationshipType,
                        StringComparison.Ordinal))
            .ToArray();

        if (classificationRelationships.Length > 1)
        {
            return LabelReadResult.Invalid();
        }

        var labelInfoRecords = Array.Empty<LabelInfoRecord>();
        if (classificationRelationships.Length == 1)
        {
            var labelInfoEntryName = ResolveInternalRelationshipTarget(
                classificationRelationships[0],
                "classification");
            var labelInfoEntry = GetPart(parts, labelInfoEntryName, required: true);
            if (labelInfoEntry is null)
            {
                return LabelReadResult.Invalid();
            }

            var labelInfo = ReadLabelInfo(labelInfoEntry);
            if (labelInfo.InvalidMetadata)
            {
                return LabelReadResult.Invalid();
            }

            labelInfoRecords = labelInfo.Records.ToArray();
        }

        var legacyLabels = ReadLegacyLabels(parts, includeDetails);
        if (legacyLabels.InvalidMetadata)
        {
            return LabelReadResult.Invalid();
        }

        var ownedTenants = labelInfoRecords
            .Where(record => record.SiteId.HasValue)
            .Select(record => record.SiteId!.Value)
            .ToHashSet();

        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var sources = new List<LabelSource>();

        // 부가 정보는 채택된 레이블만, 채택된 순서대로 모은다. LabelInfo가 이겼다면
        // LabelInfo의 값을, 레거시 속성이 살아남았다면 그쪽 값을 쓴다.
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

        foreach (var legacyLabel in legacyLabels.Labels)
        {
            if (legacyLabel.SiteId.HasValue && ownedTenants.Contains(legacyLabel.SiteId.Value))
            {
                continue;
            }

            if (seenLabelIds.Add(legacyLabel.LabelId))
            {
                labelIds.Add(legacyLabel.LabelId);
                sources.Add(LabelSource.LegacyProperty);
                if (details is not null &&
                    legacyLabels.Details.FirstOrDefault(d => d.Id == legacyLabel.LabelId) is { } detail)
                {
                    details.Add(detail);
                }
            }
        }

        return labelIds.Count == 0
            ? LabelReadResult.Unlabeled()
            : LabelReadResult.Labeled(labelIds, ProtectionType.None, details, sources);
    }

    private static LabelInfoReadResult ReadLabelInfo(ZipArchiveEntry entry)
    {
        InputGuard.CheckedLength(entry.Length, MaximumMetadataBytes, "The label metadata");

        using var stream = entry.Open();
        return LabelInfoXmlReader.Read(stream, MaximumMetadataBytes);
    }

    private static LegacyLabelReadResult ReadLegacyLabels(
        ArchivePartIndex parts,
        bool includeDetails)
    {
        var entry = GetPart(parts, CustomPropertiesEntryName, required: false);
        if (entry is null)
        {
            return new LegacyLabelReadResult([], InvalidMetadata: false, []);
        }

        InputGuard.CheckedLength(entry.Length, MaximumMetadataBytes, "The custom properties metadata");

        using var stream = entry.Open();
        var document = SafeXml.Load(stream, MaximumMetadataBytes);
        if (document.Root?.Name != CustomPropertiesNamespace + "Properties")
        {
            return new LegacyLabelReadResult([], InvalidMetadata: true, []);
        }

        var properties = new List<KeyValuePair<string, string>>();
        foreach (var property in document.Descendants(CustomPropertiesNamespace + "property"))
        {
            var propertyName = property.Attribute("name")?.Value;
            if (propertyName is null)
            {
                continue;
            }

            var value = property.Elements().FirstOrDefault()?.Value ?? string.Empty;
            properties.Add(new KeyValuePair<string, string>(propertyName, value));
        }

        var parsed = LabelPropertyParser.Parse(properties, includeDetails);
        if (parsed.InvalidMetadata)
        {
            return new LegacyLabelReadResult([], InvalidMetadata: true, []);
        }

        if (parsed.LabelIds.Count == 0)
        {
            return new LegacyLabelReadResult([], InvalidMetadata: false, []);
        }

        var activeLabelIds = new HashSet<Guid>(parsed.LabelIds);
        var siteIdsByLabelId = new Dictionary<Guid, Guid>();

        foreach (var property in properties)
        {
            if (!TryParseSiteIdKey(property.Key, out var labelId) || !activeLabelIds.Contains(labelId))
            {
                continue;
            }

            if (!Guid.TryParse(property.Value, out var siteId))
            {
                return new LegacyLabelReadResult([], InvalidMetadata: true, []);
            }

            siteIdsByLabelId[labelId] = siteId;
        }

        var labels = parsed.LabelIds
            .Select(labelId => new LegacyLabel(
                labelId,
                siteIdsByLabelId.TryGetValue(labelId, out var siteId) ? (Guid?)siteId : null))
            .ToList();

        return new LegacyLabelReadResult(labels, InvalidMetadata: false, parsed.Details);
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

    private static string ResolveInternalRelationshipTarget(
        XElement relationship,
        string relationshipName)
    {
        var targetMode = relationship.Attribute("TargetMode")?.Value;
        if (targetMode is not null &&
            !targetMode.Equals("Internal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"External {relationshipName} relationships are not allowed.");
        }

        var target = relationship.Attribute("Target")?.Value;
        return NormalizeRelationshipTarget(target, relationshipName);
    }

    private static string NormalizeRelationshipTarget(
        string? target,
        string description)
    {
        if (target?.StartsWith("/", StringComparison.Ordinal) != false)
        {
            throw new InvalidDataException($"The {description} part name must be relative.");
        }

        var firstSegmentEnd = target.IndexOf('/');
        var firstSegment = firstSegmentEnd < 0 ? target : target[..firstSegmentEnd];
        if (firstSegment.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The {description} target must not use a URI scheme.");
        }

        return CanonicalizeLogicalPartPath(target, description);
    }

    private static string NormalizeMainPartName(string? partName)
    {
        if (partName is null ||
            !partName.StartsWith("/", StringComparison.Ordinal) ||
            partName.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The main content type PartName must have exactly one leading slash.");
        }

        return CanonicalizeLogicalPartPath(partName[1..], "main content type");
    }

    private static string CanonicalizeLogicalPartPath(
        string? escapedPath,
        string description)
    {
        if (string.IsNullOrEmpty(escapedPath))
        {
            throw new InvalidDataException($"The {description} part name is invalid.");
        }

        if (escapedPath.IndexOfAny(['\\', '?', '#']) >= 0 ||
            escapedPath.Any(char.IsControl))
        {
            throw new InvalidDataException($"The {description} part name is unsafe.");
        }

        var segments = escapedPath.Split('/');
        if (segments.Any(segment => segment.Length == 0))
        {
            throw new InvalidDataException($"The {description} part name is unsafe.");
        }

        var canonicalSegments = new string[segments.Length];
        for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            var escapedSegment = segments[segmentIndex];
            if (!HasValidPercentEscapes(escapedSegment))
            {
                throw new InvalidDataException($"The {description} part name is invalid.");
            }

            var decodedSegment = Uri.UnescapeDataString(escapedSegment);
            if (decodedSegment.Equals(".", StringComparison.Ordinal) ||
                decodedSegment.Equals("..", StringComparison.Ordinal) ||
                decodedSegment.IndexOfAny(['/', '\\']) >= 0 ||
                decodedSegment.Any(char.IsControl))
            {
                throw new InvalidDataException($"The {description} part name is unsafe.");
            }

            canonicalSegments[segmentIndex] = CanonicalizeUnreservedEscapes(escapedSegment);
        }

        return string.Join('/', canonicalSegments);
    }

    private static string CanonicalizeUnreservedEscapes(string escapedSegment)
    {
        var canonical = new System.Text.StringBuilder(escapedSegment.Length);

        for (var index = 0; index < escapedSegment.Length; index++)
        {
            if (escapedSegment[index] != '%')
            {
                canonical.Append(escapedSegment[index]);
                continue;
            }

            var value = Convert.ToByte(escapedSegment.Substring(index + 1, 2), 16);
            var character = (char)value;
            if (IsUnreserved(character))
            {
                canonical.Append(character);
            }
            else
            {
                canonical.Append(escapedSegment, index, 3);
            }

            index += 2;
        }

        return canonical.ToString();
    }

    private static bool IsUnreserved(char value) =>
        value is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9' or
            '-' or '.' or '_' or '~';

    private static bool HasValidPercentEscapes(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length ||
                !Uri.IsHexDigit(value[index + 1]) ||
                !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }

    private static ArchivePartIndex CreatePartIndex(ZipArchive archive)
    {
        var parts = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var equivalentEntries = new Dictionary<string, List<ZipArchiveEntry>>(
            StringComparer.OrdinalIgnoreCase);
        var hasEquivalentDuplicates = false;

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryGetCanonicalPhysicalPartName(entry.FullName, out var logicalPartName))
            {
                continue;
            }

            if (!equivalentEntries.TryGetValue(logicalPartName, out var entries))
            {
                entries = [];
                equivalentEntries.Add(logicalPartName, entries);
            }

            entries.Add(entry);

            if (!parts.TryAdd(logicalPartName, entry))
            {
                hasEquivalentDuplicates = true;
            }
        }

        return new ArchivePartIndex(parts, equivalentEntries, hasEquivalentDuplicates);
    }

    private static bool TryGetCanonicalPhysicalPartName(
        string physicalName,
        out string logicalPartName)
    {
        try
        {
            logicalPartName = CanonicalizeLogicalPartPath(physicalName, "physical ZIP");
            return logicalPartName.Equals(physicalName, StringComparison.Ordinal);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or UriFormatException)
        {
            logicalPartName = string.Empty;
            return false;
        }
    }

    private static ZipArchiveEntry? GetPart(
        ArchivePartIndex parts,
        string entryName,
        bool required)
    {
        if (!parts.TryGetValue(entryName, out var entry) && required)
        {
            throw new InvalidDataException($"The package does not contain '{entryName}'.");
        }

        return entry;
    }

    private static bool IsWithinStructuralBudgets(
        IReadOnlyList<ZipArchiveEntry> contentTypeEntries,
        IReadOnlyList<ZipArchiveEntry> relationshipEntries)
    {
        try
        {
            var entryCount = checked(contentTypeEntries.Count + relationshipEntries.Count);
            if (entryCount > MaxStructuralEntryCount)
            {
                return false;
            }

            long aggregateBytes = 0;
            foreach (var entry in contentTypeEntries.Concat(relationshipEntries))
            {
                aggregateBytes = checked(aggregateBytes + entry.Length);
                if (aggregateBytes > MaxAggregateStructuralBytes)
                {
                    return false;
                }
            }

            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static MetadataDocument LoadMetadataDocument(ZipArchiveEntry entry)
    {
        if (entry.Length > MaximumMetadataBytes)
        {
            return MetadataDocument.Invalid();
        }

        try
        {
            using var stream = entry.Open();
            return MetadataDocument.Valid(SafeXml.Load(stream, MaximumMetadataBytes));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException)
        {
            return MetadataDocument.Invalid();
        }
    }

    private static StructuralInspection<ContentTypeEvidence> InspectContentTypes(
        IReadOnlyList<ZipArchiveEntry> entries)
    {
        if (entries.Count == 0)
        {
            return new StructuralInspection<ContentTypeEvidence>(
                ContentTypeEvidence.Invalid,
                HasInvalidMetadata: true);
        }

        var selectedEvidence = ContentTypeEvidence.Invalid;
        var hasInvalidMetadata = false;
        foreach (var entry in entries)
        {
            var inspection = InspectContentTypesEntry(entry);
            selectedEvidence = inspection.Evidence;
            hasInvalidMetadata |= inspection.HasInvalidMetadata;
            if (selectedEvidence.HasOfficeEvidence)
            {
                break;
            }
        }

        return new StructuralInspection<ContentTypeEvidence>(
            selectedEvidence,
            hasInvalidMetadata);
    }

    private static StructuralInspection<ContentTypeEvidence> InspectContentTypesEntry(
        ZipArchiveEntry entry)
    {
        var metadata = LoadMetadataDocument(entry);
        return new StructuralInspection<ContentTypeEvidence>(
            InspectContentTypesDocument(metadata),
            HasInvalidMetadata: !metadata.IsValid);
    }

    private static ContentTypeEvidence InspectContentTypesDocument(MetadataDocument metadata)
    {
        if (!metadata.IsValid || metadata.Document?.Root is not { } root)
        {
            return ContentTypeEvidence.Invalid;
        }

        if (root.Name != ContentTypesNamespace + "Types")
        {
            return ContentTypeEvidence.Invalid;
        }

        var mainOverrides = root
            .Elements(ContentTypesNamespace + "Override")
            .Where(
                element =>
                    element.Attribute("ContentType")?.Value is { } contentType &&
                    OfficeMainContentTypes.Contains(contentType))
            .ToArray();

        if (mainOverrides.Length == 0)
        {
            return new ContentTypeEvidence(
                HasValidStructure: true,
                HasOfficeEvidence: false,
                HasValidCandidate: false,
                MainPartName: null);
        }

        if (mainOverrides.Length != 1)
        {
            return new ContentTypeEvidence(
                HasValidStructure: true,
                HasOfficeEvidence: true,
                HasValidCandidate: false,
                MainPartName: null);
        }

        var mainPartName = NormalizeMainPartName(
            mainOverrides[0].Attribute("PartName")?.Value);
        return new ContentTypeEvidence(
            HasValidStructure: true,
            HasOfficeEvidence: true,
            HasValidCandidate: true,
            mainPartName);
    }

    private static StructuralInspection<RelationshipEvidence> InspectRelationships(
        IReadOnlyList<ZipArchiveEntry> entries)
    {
        if (entries.Count == 0)
        {
            return new StructuralInspection<RelationshipEvidence>(
                RelationshipEvidence.Invalid,
                HasInvalidMetadata: true);
        }

        var selectedEvidence = RelationshipEvidence.Invalid;
        var hasInvalidMetadata = false;
        foreach (var entry in entries)
        {
            var inspection = InspectRelationshipsEntry(entry);
            selectedEvidence = inspection.Evidence;
            hasInvalidMetadata |= inspection.HasInvalidMetadata;
            if (selectedEvidence.HasOfficeEvidence)
            {
                break;
            }
        }

        return new StructuralInspection<RelationshipEvidence>(
            selectedEvidence,
            hasInvalidMetadata);
    }

    private static StructuralInspection<RelationshipEvidence> InspectRelationshipsEntry(
        ZipArchiveEntry entry)
    {
        var metadata = LoadMetadataDocument(entry);
        return new StructuralInspection<RelationshipEvidence>(
            InspectRelationshipsDocument(metadata),
            HasInvalidMetadata: !metadata.IsValid);
    }

    private static RelationshipEvidence InspectRelationshipsDocument(MetadataDocument metadata)
    {
        if (!metadata.IsValid || metadata.Document?.Root is not { } root)
        {
            return RelationshipEvidence.Invalid;
        }

        if (root.Name != RelationshipsNamespace + "Relationships")
        {
            return RelationshipEvidence.Invalid;
        }

        var relationships = root.Elements(RelationshipsNamespace + "Relationship").ToArray();
        var officeDocumentRelationships = relationships
            .Where(
                relationship =>
                    relationship.Attribute("Type")?.Value is { } relationshipType &&
                    OfficeDocumentRelationshipTypes.Contains(relationshipType))
            .ToArray();

        if (officeDocumentRelationships.Length == 0)
        {
            return new RelationshipEvidence(
                HasValidStructure: true,
                HasOfficeEvidence: false,
                HasValidCandidate: false,
                MainPartName: null,
                relationships);
        }

        if (officeDocumentRelationships.Length != 1)
        {
            return new RelationshipEvidence(
                HasValidStructure: true,
                HasOfficeEvidence: true,
                HasValidCandidate: false,
                MainPartName: null,
                relationships);
        }

        var mainPartName = ResolveInternalRelationshipTarget(
            officeDocumentRelationships[0],
            "officeDocument");
        return new RelationshipEvidence(
            HasValidStructure: true,
            HasOfficeEvidence: true,
            HasValidCandidate: true,
            mainPartName,
            relationships);
    }

    private sealed class ArchivePartIndex(
        Dictionary<string, ZipArchiveEntry> parts,
        Dictionary<string, List<ZipArchiveEntry>> equivalentEntries,
        bool hasEquivalentDuplicates)
    {
        private readonly Dictionary<string, ZipArchiveEntry> _parts = parts;
        private readonly Dictionary<string, List<ZipArchiveEntry>> _equivalentEntries =
            equivalentEntries;

        internal bool HasEquivalentDuplicates { get; } = hasEquivalentDuplicates;

        internal IReadOnlyList<ZipArchiveEntry> GetEquivalentEntries(string partName) =>
            _equivalentEntries.TryGetValue(partName, out var entries)
                ? entries
                : [];

        internal bool TryGetValue(string partName, out ZipArchiveEntry entry)
        {
            if (_parts.TryGetValue(partName, out var found))
            {
                entry = found;
                return true;
            }

            entry = null!;
            return false;
        }
    }

    private sealed record LegacyLabel(Guid LabelId, Guid? SiteId);

    private sealed record LegacyLabelReadResult(
        IReadOnlyList<LegacyLabel> Labels,
        bool InvalidMetadata,
        IReadOnlyList<LabelDetail> Details);

    private sealed record StructuralInspection<TEvidence>(
        TEvidence Evidence,
        bool HasInvalidMetadata);

    private sealed record MetadataDocument(bool IsValid, XDocument? Document)
    {
        internal static MetadataDocument Invalid() => new(IsValid: false, null);

        internal static MetadataDocument Valid(XDocument document) => new(IsValid: true, document);
    }

    private sealed record ContentTypeEvidence(
        bool HasValidStructure,
        bool HasOfficeEvidence,
        bool HasValidCandidate,
        string? MainPartName)
    {
        internal static ContentTypeEvidence Invalid { get; } =
            new(false, false, false, null);
    }

    private sealed record RelationshipEvidence(
        bool HasValidStructure,
        bool HasOfficeEvidence,
        bool HasValidCandidate,
        string? MainPartName,
        IReadOnlyList<XElement> Relationships)
    {
        internal static RelationshipEvidence Invalid { get; } =
            new(false, false, false, null, []);
    }
}
