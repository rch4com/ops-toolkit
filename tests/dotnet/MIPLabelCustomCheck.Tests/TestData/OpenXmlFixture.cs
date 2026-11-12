using System.IO.Compression;
using System.Security;
using System.Text;

namespace MIPLabelCustomCheck.Tests.TestData;

internal enum OpenXmlDocumentKind
{
    Word,
    Excel,
    PowerPoint
}

internal sealed record LabelInfoValue(
    string LabelId,
    string? SiteId = null,
    string Enabled = "true",
    string Removed = "false");

internal sealed record CustomPropertyValue(
    string Name,
    string Value,
    string ValueElement = "lpwstr");

internal sealed class OpenXmlFixture
{
    /// <summary>Mirrors <c>OpenXmlLabelReader.MaximumMetadataBytes</c> for oversized-entry fixtures.</summary>
    internal const int MaximumMetadataBytes = 1024 * 1024;

    internal const string ClassificationRelationshipType =
        "http://schemas.microsoft.com/office/2020/02/relationships/classificationlabels";

    internal const string TransitionalOfficeDocumentRelationshipType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";

    internal const string StrictOfficeDocumentRelationshipType =
        "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument";

    internal const string LabelNamespace =
        "http://schemas.microsoft.com/office/2020/mipLabelMetadata";

    private const string RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string CustomPropertiesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

    private const string ValueTypesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

    private readonly List<ClassificationRelationship> _classificationRelationships =
    [
        new("docMetadata/LabelInfo.xml", null)
    ];

    private readonly List<OfficeDocumentRelationship> _officeDocumentRelationships =
    [
        new(
            TransitionalOfficeDocumentRelationshipType,
            Target: null,
            TargetMode: null,
            UseDefaultTarget: true)
    ];

    private readonly List<MainContentTypeOverride> _mainContentTypeOverrides =
    [
        new(PartName: null, ContentType: null)
    ];

    private readonly List<(string Name, string Content)> _additionalEntries = [];
    private OpenXmlDocumentKind _documentKind = OpenXmlDocumentKind.Word;
    private bool _includeContentTypes = true;
    private string? _contentTypesXml;
    private bool _includeRelationships = true;
    private string? _relationshipsXml;
    private bool _includeMainDocument = true;
    private string? _mainDocumentEntryName;
    private string? _mainDocumentXml;
    private bool _includeLabelInfoEntry = true;
    private string _labelInfoEntryName = "docMetadata/LabelInfo.xml";
    private string _labelInfoXml = LabelInfo(
        new LabelInfoValue("6333fd3f-635c-44a8-824c-7f4faab3944a"));
    private string? _customPropertiesXml;

    internal OpenXmlFixture WithDocumentKind(OpenXmlDocumentKind kind)
    {
        _documentKind = kind;
        return this;
    }

    internal OpenXmlFixture WithClassificationTarget(string target)
    {
        _classificationRelationships.Clear();
        _classificationRelationships.Add(new ClassificationRelationship(target, null));
        _labelInfoEntryName = target;
        return this;
    }

    internal OpenXmlFixture WithClassificationRelationshipTarget(
        string target,
        string? targetMode = null)
    {
        _classificationRelationships.Clear();
        _classificationRelationships.Add(new ClassificationRelationship(target, targetMode));
        return this;
    }

    internal OpenXmlFixture AddClassificationRelationship(
        string target,
        string? targetMode = null)
    {
        _classificationRelationships.Add(new ClassificationRelationship(target, targetMode));
        return this;
    }

    internal OpenXmlFixture WithoutClassificationRelationships()
    {
        _classificationRelationships.Clear();
        return this;
    }

    internal OpenXmlFixture WithLabelInfo(params LabelInfoValue[] labels)
    {
        _includeLabelInfoEntry = true;
        _labelInfoXml = LabelInfo(labels);
        return this;
    }

    internal OpenXmlFixture WithLabelInfoXml(string xml)
    {
        _includeLabelInfoEntry = true;
        _labelInfoXml = xml;
        return this;
    }

    internal OpenXmlFixture WithLabelInfoEntryName(string entryName)
    {
        _labelInfoEntryName = entryName;
        return this;
    }

    /// <summary>
    /// Writes a LabelInfo entry one byte past <see cref="MaximumMetadataBytes"/>, the production
    /// reader's declared-length cap, so it is rejected before its XML is even parsed.
    /// </summary>
    internal OpenXmlFixture WithOversizedLabelInfo()
    {
        var padding = new string(' ', MaximumMetadataBytes + 1);
        return WithLabelInfoXml($"<labelInfo xmlns=\"{LabelNamespace}\">{padding}</labelInfo>");
    }

    /// <summary>
    /// Points the root office-document relationship at an external target, which the reader must
    /// reject as structurally invalid rather than silently ignore.
    /// </summary>
    internal OpenXmlFixture WithExternalRelationship() =>
        WithOfficeDocumentRelationshipTarget(MainDocumentPath, "External");

    internal OpenXmlFixture WithoutLabelInfoEntry()
    {
        _includeLabelInfoEntry = false;
        return this;
    }

    internal OpenXmlFixture WithCustomProperties(params CustomPropertyValue[] properties)
    {
        _customPropertiesXml = CustomProperties(properties);
        return this;
    }

    internal OpenXmlFixture WithCustomPropertiesXml(string xml)
    {
        _customPropertiesXml = xml;
        return this;
    }

    internal OpenXmlFixture WithContentTypesXml(string xml)
    {
        _includeContentTypes = true;
        _contentTypesXml = xml;
        return this;
    }

    internal OpenXmlFixture WithoutContentTypes()
    {
        _includeContentTypes = false;
        return this;
    }

    internal OpenXmlFixture WithRelationshipsXml(string xml)
    {
        _includeRelationships = true;
        _relationshipsXml = xml;
        return this;
    }

    internal OpenXmlFixture WithoutRelationships()
    {
        _includeRelationships = false;
        return this;
    }

    internal OpenXmlFixture WithoutOfficeDocumentRelationship()
    {
        _officeDocumentRelationships.Clear();
        return this;
    }

    internal OpenXmlFixture WithOfficeDocumentRelationshipTarget(
        string? target,
        string? targetMode = null)
    {
        _officeDocumentRelationships.Clear();
        _officeDocumentRelationships.Add(
            new OfficeDocumentRelationship(
                TransitionalOfficeDocumentRelationshipType,
                target,
                targetMode,
                UseDefaultTarget: false));
        return this;
    }

    internal OpenXmlFixture WithStrictOfficeDocumentRelationship()
    {
        _officeDocumentRelationships.Clear();
        _officeDocumentRelationships.Add(
            new OfficeDocumentRelationship(
                StrictOfficeDocumentRelationshipType,
                Target: null,
                TargetMode: null,
                UseDefaultTarget: true));
        return this;
    }

    internal OpenXmlFixture AddOfficeDocumentRelationship(
        string target,
        string? targetMode = null)
    {
        _officeDocumentRelationships.Add(
            new OfficeDocumentRelationship(
                TransitionalOfficeDocumentRelationshipType,
                target,
                targetMode,
                UseDefaultTarget: false));
        return this;
    }

    internal OpenXmlFixture WithoutRecognizedMainOverride()
    {
        _mainContentTypeOverrides.Clear();
        return this;
    }

    internal OpenXmlFixture WithRecognizedMainOverridePartName(string partName)
    {
        _mainContentTypeOverrides.Clear();
        _mainContentTypeOverrides.Add(new MainContentTypeOverride(partName, null));
        return this;
    }

    internal OpenXmlFixture AddRecognizedMainOverride(string partName)
    {
        _mainContentTypeOverrides.Add(new MainContentTypeOverride(partName, null));
        return this;
    }

    internal OpenXmlFixture WithMainDocumentXml(string xml)
    {
        _includeMainDocument = true;
        _mainDocumentXml = xml;
        return this;
    }

    internal OpenXmlFixture WithoutMainDocument()
    {
        _includeMainDocument = false;
        return this;
    }

    internal OpenXmlFixture WithMainDocumentEntryName(string entryName)
    {
        _includeMainDocument = true;
        _mainDocumentEntryName = entryName;
        return this;
    }

    internal OpenXmlFixture AddEntry(string name, string content)
    {
        _additionalEntries.Add((name, content));
        return this;
    }

    internal MemoryStream Create()
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (_includeContentTypes)
            {
                WriteEntry(
                    archive,
                    "[Content_Types].xml",
                    _contentTypesXml ?? CreateContentTypesXml());
            }

            if (_includeRelationships)
            {
                WriteEntry(
                    archive,
                    "_rels/.rels",
                    _relationshipsXml ?? CreateRelationshipsXml());
            }

            if (_includeMainDocument)
            {
                WriteEntry(
                    archive,
                    _mainDocumentEntryName ?? MainDocumentPath,
                    _mainDocumentXml ?? CreateMainDocumentXml());
            }

            if (_includeLabelInfoEntry)
            {
                WriteEntry(archive, _labelInfoEntryName, _labelInfoXml);
            }

            if (_customPropertiesXml is not null)
            {
                WriteEntry(archive, "docProps/custom.xml", _customPropertiesXml);
            }

            foreach (var (name, content) in _additionalEntries)
            {
                WriteEntry(archive, name, content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    internal static string LabelInfo(params LabelInfoValue[] labels)
    {
        var body = new StringBuilder();
        foreach (var label in labels)
        {
            var siteId = label.SiteId is null
                ? string.Empty
                : $" siteId=\"{Escape(label.SiteId)}\"";

            body.Append(
                $"<label id=\"{Escape(label.LabelId)}\"{siteId} " +
                $"enabled=\"{Escape(label.Enabled)}\" removed=\"{Escape(label.Removed)}\" />");
        }

        return $"<labelInfo xmlns=\"{LabelNamespace}\">{body}</labelInfo>";
    }

    internal static string CustomProperties(params CustomPropertyValue[] properties)
    {
        var body = new StringBuilder();
        for (var index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            body.Append(
                $"<property fmtid=\"{{D5CDD505-2E9C-101B-9397-08002B2CF9AE}}\" " +
                $"pid=\"{index + 2}\" name=\"{Escape(property.Name)}\">" +
                $"<vt:{property.ValueElement}>{Escape(property.Value)}</vt:{property.ValueElement}>" +
                "</property>");
        }

        return
            $"<Properties xmlns=\"{CustomPropertiesNamespace}\" xmlns:vt=\"{ValueTypesNamespace}\">" +
            body +
            "</Properties>";
    }

    internal static string GenericContentTypesXml() =>
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\" />" +
        "</Types>";

    private string MainDocumentPath => _documentKind switch
    {
        OpenXmlDocumentKind.Word => "word/document.xml",
        OpenXmlDocumentKind.Excel => "xl/workbook.xml",
        OpenXmlDocumentKind.PowerPoint => "ppt/presentation.xml",
        _ => throw new InvalidOperationException()
    };

    private string MainDocumentContentType => _documentKind switch
    {
        OpenXmlDocumentKind.Word =>
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
        OpenXmlDocumentKind.Excel =>
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
        OpenXmlDocumentKind.PowerPoint =>
            "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
        _ => throw new InvalidOperationException()
    };

    private string CreateContentTypesXml()
    {
        var body = new StringBuilder();
        foreach (var mainOverride in _mainContentTypeOverrides)
        {
            body.Append(
                $"<Override PartName=\"{Escape(mainOverride.PartName ?? $"/{MainDocumentPath}")}\" " +
                $"ContentType=\"{Escape(mainOverride.ContentType ?? MainDocumentContentType)}\" />");
        }

        return
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\" />" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\" />" +
            body +
            "</Types>";
    }

    private string CreateRelationshipsXml()
    {
        var body = new StringBuilder();
        var relationshipId = 1;

        foreach (var relationship in _officeDocumentRelationships)
        {
            var target = relationship.UseDefaultTarget
                ? $" Target=\"{Escape(MainDocumentPath)}\""
                : relationship.Target is null
                    ? string.Empty
                    : $" Target=\"{Escape(relationship.Target)}\"";
            var targetMode = relationship.TargetMode is null
                ? string.Empty
                : $" TargetMode=\"{Escape(relationship.TargetMode)}\"";

            body.Append(
                $"<Relationship Id=\"rId{relationshipId++}\" " +
                $"Type=\"{Escape(relationship.Type)}\" " +
                $"{target}{targetMode} />");
        }

        foreach (var relationship in _classificationRelationships)
        {
            var targetMode = relationship.TargetMode is null
                ? string.Empty
                : $" TargetMode=\"{Escape(relationship.TargetMode)}\"";

            body.Append(
                $"<Relationship Id=\"rId{relationshipId++}\" " +
                $"Type=\"{ClassificationRelationshipType}\" " +
                $"Target=\"{Escape(relationship.Target)}\"{targetMode} />");
        }

        return $"<Relationships xmlns=\"{RelationshipsNamespace}\">{body}</Relationships>";
    }

    private string CreateMainDocumentXml() => _documentKind switch
    {
        OpenXmlDocumentKind.Word =>
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body /></w:document>",
        OpenXmlDocumentKind.Excel =>
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" />",
        OpenXmlDocumentKind.PowerPoint =>
            "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" />",
        _ => throw new InvalidOperationException()
    };

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private sealed record ClassificationRelationship(string Target, string? TargetMode);

    private sealed record OfficeDocumentRelationship(
        string Type,
        string? Target,
        string? TargetMode,
        bool UseDefaultTarget);

    private sealed record MainContentTypeOverride(string? PartName, string? ContentType);
}
