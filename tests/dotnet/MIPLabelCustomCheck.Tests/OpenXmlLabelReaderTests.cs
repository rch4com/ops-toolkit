using MIPLabelCustomCheck.Internal;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class OpenXmlLabelReaderTests
{
    private const int MetadataMaximumBytes = 1024 * 1024;
    private const string RelationshipsNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string CustomPropertiesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
    private const string ValueTypesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

    private const string OfficeMainContentTypesXml =
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Override PartName=\"/word/document.xml\" " +
        "ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\" />" +
        "</Types>";

    private const string GenericRootRelationshipsXml =
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\" />";

    private const string OfficeRootRelationshipsXml =
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" " +
        "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" " +
        "Target=\"word/document.xml\" />" +
        "</Relationships>";

    private static readonly Guid FirstLabelId =
        Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");
    private static readonly Guid SecondLabelId =
        Guid.Parse("e19cdb69-0515-4f99-a455-60417f2e080d");
    private static readonly Guid FirstSiteId =
        Guid.Parse("60bf846f-b960-4abf-b752-5086ae3870a8");
    private static readonly Guid SecondSiteId =
        Guid.Parse("2072793a-2c67-49bf-bf25-42092d64b0cb");

    public static TheoryData<string, string?> UnsafeClassificationTargets =>
        new()
        {
            { "docMetadata/LabelInfo.xml", "External" },
            { "/docMetadata/LabelInfo.xml", null },
            { "https://example.test/LabelInfo.xml", null },
            { "metadata:LabelInfo.xml", null },
            { "docMetadata\\LabelInfo.xml", null },
            { "docMetadata/%5cLabelInfo.xml", null },
            { "docMetadata/%2fLabelInfo.xml", null },
            { "docMetadata/../LabelInfo.xml", null },
            { "docMetadata/%2e%2e/LabelInfo.xml", null },
            { "docMetadata/%00LabelInfo.xml", null },
            { "docMetadata/%1fLabelInfo.xml", null },
            { "docMetadata/%ZZLabelInfo.xml", null },
            { "docMetadata/%LabelInfo.xml", null },
            { "docMetadata//LabelInfo.xml", null },
            { "docMetadata/LabelInfo.xml?download=1", null },
            { "docMetadata/LabelInfo.xml#label", null }
        };

    public static TheoryData<string?, string?> UnsafeOfficeDocumentTargets =>
        new()
        {
            { null, null },
            { "word/document.xml", "External" },
            { "/word/document.xml", null },
            { "https://example.test/document.xml", null },
            { "word:document.xml", null },
            { "word\\document.xml", null },
            { "word/%5cdocument.xml", null },
            { "word/%2fdocument.xml", null },
            { "word/../document.xml", null },
            { "word/%2e%2e/document.xml", null },
            { "word/%00document.xml", null },
            { "word/%1fdocument.xml", null },
            { "word/%ZZdocument.xml", null },
            { "word/%document.xml", null },
            { "word//document.xml", null },
            { "word/./document.xml", null },
            { "word/document.xml?download=1", null },
            { "word/document.xml#main", null }
        };

    public static TheoryData<string> UnsafeMainOverridePartNames =>
        new()
        {
            "//word/document.xml",
            "/https://example.test/document.xml",
            "/word:document.xml",
            "/word\\document.xml",
            "/word/%5cdocument.xml",
            "/word/%2fdocument.xml",
            "/word/../document.xml",
            "/word/%2e%2e/document.xml",
            "/word/%00document.xml",
            "/word/%1fdocument.xml",
            "/word/%ZZdocument.xml",
            "/word/%document.xml",
            "/word//document.xml",
            "/word/./document.xml",
            "/word/document.xml?download=1",
            "/word/document.xml#main"
        };

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    public void Read_real_office_fixture_returns_expected_label(string extension)
    {
        using var stream = File.OpenRead(FixturePaths.Get(extension));

        var result = OpenXmlLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    public void Check_dispatches_real_office_fixture_and_preserves_caller_stream(string extension)
    {
        using var stream = File.OpenRead(FixturePaths.Get(extension));
        stream.Position = 11;

        var result = new MipLabelChecker().Check(stream, $"sample{extension}");

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
        Assert.True(stream.CanRead);
        Assert.Equal(11, stream.Position);
    }

    [Fact]
    public void Read_resolves_non_default_classification_relationship_target()
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationTarget("metadata/custom-label.xml")
            .WithLabelInfo(new LabelInfoValue(FirstLabelId.ToString("D")))
            .Create();

        var result = OpenXmlLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Theory]
    [InlineData("docMetadata/Label%20Info.xml")]
    [InlineData("docMetadata/Label%25Info.xml")]
    [InlineData("docMetadata/%252e.xml")]
    [InlineData("docMetadata/%252f.xml")]
    public void Read_preserves_escaped_classification_part_identity(string target)
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationTarget(target)
            .Create();

        var result = OpenXmlLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_canonicalizes_unreserved_escape_to_physical_literal_part()
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationRelationshipTarget("docMetadata/Label%49nfo.xml")
            .Create();

        Assert.Equal(new[] { FirstLabelId }, OpenXmlLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_does_not_map_noncanonical_unreserved_escape_physical_part()
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationTarget("docMetadata/Label%49nfo.xml")
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData("folder/foo:bar.xml")]
    [InlineData("scheme%3Aname/LabelInfo.xml")]
    public void Read_accepts_non_scheme_colon_classification_target(string target)
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationTarget(target)
            .Create();

        Assert.Equal(new[] { FirstLabelId }, OpenXmlLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_resolves_classification_part_case_insensitively()
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationRelationshipTarget("DOCMETADATA/LABELINFO.XML")
            .Create();

        Assert.Equal(new[] { FirstLabelId }, OpenXmlLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_returns_invalid_for_case_equivalent_classification_part_duplicates()
    {
        using var stream = new OpenXmlFixture()
            .AddEntry(
                "DOCMETADATA/LABELINFO.XML",
                OpenXmlFixture.LabelInfo(
                    new LabelInfoValue(SecondLabelId.ToString("D"))))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_removed_label_info_tenant_suppresses_same_tenant_legacy_label()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D"),
                    Enabled: "true",
                    Removed: "true"))
            .WithCustomProperties(LegacyProperties(SecondLabelId, FirstSiteId))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_disabled_label_info_tenant_suppresses_same_tenant_legacy_label()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D"),
                    Enabled: "false"))
            .WithCustomProperties(LegacyProperties(SecondLabelId, FirstSiteId))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_keeps_legacy_label_from_tenant_not_owned_by_label_info()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D"),
                    Enabled: "false"))
            .WithCustomProperties(LegacyProperties(SecondLabelId, SecondSiteId))
            .Create();

        var result = OpenXmlLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { SecondLabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_orders_active_label_info_before_distinct_tenant_legacy_labels()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D")))
            .WithCustomProperties(LegacyProperties(SecondLabelId, SecondSiteId))
            .Create();

        var result = OpenXmlLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(new[] { FirstLabelId, SecondLabelId }, result.LabelIds);
    }

    [Theory]
    [InlineData("lpwstr", "true")]
    [InlineData("lpstr", "TRUE")]
    [InlineData("bool", "1")]
    [InlineData("int", "1")]
    public void Read_accepts_first_custom_property_value_element_text(
        string valueElement,
        string enabledValue)
    {
        using var stream = LegacyOnlyFixture()
            .WithCustomProperties(
                new CustomPropertyValue(
                    $"MSIP_Label_{FirstLabelId:D}_Enabled",
                    enabledValue,
                    valueElement))
            .Create();

        Assert.Equal(new[] { FirstLabelId }, OpenXmlLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_uses_only_first_value_element_of_custom_property()
    {
        var xml =
            $"<Properties xmlns=\"{CustomPropertiesNamespace}\" xmlns:vt=\"{ValueTypesNamespace}\">" +
            $"<property name=\"MSIP_Label_{FirstLabelId:D}_Enabled\">" +
            "<vt:bool>true</vt:bool><vt:lpwstr>false</vt:lpwstr>" +
            "</property></Properties>";
        using var stream = LegacyOnlyFixture().WithCustomPropertiesXml(xml).Create();

        Assert.Equal(new[] { FirstLabelId }, OpenXmlLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_returns_invalid_for_active_custom_property_with_malformed_label_id()
    {
        using var stream = LegacyOnlyFixture()
            .WithCustomProperties(
                new CustomPropertyValue("MSIP_Label_not-a-guid_Enabled", "true"))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_active_custom_property_with_malformed_site_id()
    {
        using var stream = LegacyOnlyFixture()
            .WithCustomProperties(
                new CustomPropertyValue($"MSIP_Label_{FirstLabelId:D}_Enabled", "true"),
                new CustomPropertyValue($"MSIP_Label_{FirstLabelId:D}_SiteId", "not-a-guid"))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_ignores_inactive_custom_property_with_malformed_label_id()
    {
        using var stream = LegacyOnlyFixture()
            .WithCustomProperties(
                new CustomPropertyValue("MSIP_Label_not-a-guid_Enabled", "false"))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_keeps_legacy_label_without_site_id_despite_owned_label_info_tenant()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D"),
                    Enabled: "false"))
            .WithCustomProperties(
                new CustomPropertyValue($"MSIP_Label_{SecondLabelId:D}_Enabled", "true"))
            .Create();

        Assert.Equal(new[] { SecondLabelId }, OpenXmlLabelReader.Read(stream).LabelIds);
    }

    [Fact]
    public void Read_groups_legacy_properties_by_parsed_guid_for_tenant_suppression()
    {
        var upperLabelId = SecondLabelId.ToString("D").ToUpperInvariant();
        var bracedLabelId = SecondLabelId.ToString("B").ToLowerInvariant();
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D"),
                    Enabled: "false"))
            .WithCustomProperties(
                new CustomPropertyValue($"MSIP_Label_{upperLabelId}_Enabled", "true"),
                new CustomPropertyValue(
                    $"MSIP_Label_{bracedLabelId}_SiteId",
                    FirstSiteId.ToString("D")))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_validates_malformed_site_id_joined_by_parsed_label_guid()
    {
        var upperLabelId = FirstLabelId.ToString("D").ToUpperInvariant();
        var compactLabelId = FirstLabelId.ToString("N").ToLowerInvariant();
        using var stream = LegacyOnlyFixture()
            .WithCustomProperties(
                new CustomPropertyValue($"MSIP_Label_{upperLabelId}_Enabled", "true"),
                new CustomPropertyValue(
                    $"MSIP_Label_{compactLabelId}_SiteId",
                    "not-a-guid"))
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_ignores_malformed_site_id_for_inactive_parsed_label_group()
    {
        var upperLabelId = FirstLabelId.ToString("D").ToUpperInvariant();
        var compactLabelId = FirstLabelId.ToString("N").ToLowerInvariant();
        using var stream = LegacyOnlyFixture()
            .WithCustomProperties(
                new CustomPropertyValue($"MSIP_Label_{upperLabelId}_Enabled", "false"),
                new CustomPropertyValue(
                    $"MSIP_Label_{compactLabelId}_SiteId",
                    "not-a-guid"))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unsupported_for_valid_non_office_zip()
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithoutRelationships()
            .WithoutMainDocument()
            .WithoutLabelInfoEntry()
            .AddEntry("data/value.xml", "<value />")
            .Create();

        Assert.Equal(LabelStatus.Unsupported, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unsupported_for_generic_zip_with_case_distinct_entries()
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithoutRelationships()
            .WithoutMainDocument()
            .WithoutLabelInfoEntry()
            .AddEntry("data.txt", "lower")
            .AddEntry("DATA.TXT", "upper")
            .Create();

        Assert.Equal(LabelStatus.Unsupported, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_office_zip_with_unrelated_equivalent_duplicates()
    {
        using var stream = new OpenXmlFixture()
            .AddEntry("data.txt", "lower")
            .AddEntry("DATA.TXT", "upper")
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_finds_office_evidence_in_any_duplicate_content_types_entry(
        bool officeEntryFirst)
    {
        var fixture = new OpenXmlFixture()
            .WithoutRelationships()
            .WithoutLabelInfoEntry();

        if (officeEntryFirst)
        {
            fixture
                .WithContentTypesXml(OfficeMainContentTypesXml)
                .AddEntry("[CONTENT_TYPES].XML", OpenXmlFixture.GenericContentTypesXml());
        }
        else
        {
            fixture
                .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
                .AddEntry("[CONTENT_TYPES].XML", OfficeMainContentTypesXml);
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_finds_office_evidence_in_any_duplicate_root_relationships_entry(
        bool officeEntryFirst)
    {
        var fixture = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithoutLabelInfoEntry();

        if (officeEntryFirst)
        {
            fixture
                .WithRelationshipsXml(OfficeRootRelationshipsXml)
                .AddEntry("_RELS/.RELS", GenericRootRelationshipsXml);
        }
        else
        {
            fixture
                .WithRelationshipsXml(GenericRootRelationshipsXml)
                .AddEntry("_RELS/.RELS", OfficeRootRelationshipsXml);
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unsupported_for_duplicate_structural_entries_without_office_evidence()
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithRelationshipsXml(GenericRootRelationshipsXml)
            .WithoutMainDocument()
            .WithoutLabelInfoEntry()
            .AddEntry("[CONTENT_TYPES].XML", OpenXmlFixture.GenericContentTypesXml())
            .AddEntry("_RELS/.RELS", GenericRootRelationshipsXml)
            .Create();

        Assert.Equal(LabelStatus.Unsupported, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_structural_entry_count_exceeds_64()
    {
        var fixture = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithRelationshipsXml(GenericRootRelationshipsXml)
            .WithoutMainDocument()
            .WithoutLabelInfoEntry();

        for (var index = 0; index < 32; index++)
        {
            fixture.AddEntry("[CONTENT_TYPES].XML", OpenXmlFixture.GenericContentTypesXml());
        }

        for (var index = 0; index < 31; index++)
        {
            fixture.AddEntry("_RELS/.RELS", GenericRootRelationshipsXml);
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_aggregate_structural_bytes_exceed_4_mib()
    {
        const int paddingCharacters = 850 * 1024;
        var largeContentTypes = LargeGenericContentTypesXml(paddingCharacters);
        var largeRelationships = LargeGenericRelationshipsXml(paddingCharacters);
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(largeContentTypes)
            .WithRelationshipsXml(largeRelationships)
            .WithoutMainDocument()
            .WithoutLabelInfoEntry()
            .AddEntry("[CONTENT_TYPES].XML", largeContentTypes)
            .AddEntry("[CONTENT_TYPES].XML", largeContentTypes)
            .AddEntry("_RELS/.RELS", largeRelationships)
            .AddEntry("_RELS/.RELS", largeRelationships)
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unsupported_for_non_office_zip_without_content_types()
    {
        using var stream = new OpenXmlFixture()
            .WithoutContentTypes()
            .WithoutRelationships()
            .WithoutMainDocument()
            .WithoutLabelInfoEntry()
            .AddEntry("data.txt", "not Office")
            .Create();

        Assert.Equal(LabelStatus.Unsupported, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unsupported_for_clearly_non_office_zip_with_corrupt_content_types()
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml("<Types")
            .WithoutRelationships()
            .WithoutMainDocument()
            .WithoutLabelInfoEntry()
            .AddEntry("data.txt", "not Office")
            .Create();

        Assert.Equal(LabelStatus.Unsupported, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_malformed_content_types_with_office_relationship_evidence()
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml("<Types")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_missing_content_types_with_office_relationship_evidence()
    {
        using var stream = new OpenXmlFixture()
            .WithoutContentTypes()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_office_relationship_has_no_recognized_main_override()
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_recognized_content_type_with_empty_root_relationships()
    {
        using var stream = new OpenXmlFixture()
            .WithoutOfficeDocumentRelationship()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .WithoutMainDocument()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_classification_relationship_without_office_document_part()
    {
        using var stream = new OpenXmlFixture()
            .WithoutOfficeDocumentRelationship()
            .WithoutMainDocument()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("word/document.xml", "External")]
    [InlineData("../word/document.xml", null)]
    public void Read_returns_invalid_for_generic_content_types_with_unsafe_office_relationship(
        string? target,
        string? targetMode)
    {
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(OpenXmlFixture.GenericContentTypesXml())
            .WithOfficeDocumentRelationshipTarget(target, targetMode)
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_multiple_root_office_document_relationships()
    {
        using var stream = new OpenXmlFixture()
            .AddOfficeDocumentRelationship("word/document.xml")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [MemberData(nameof(UnsafeOfficeDocumentTargets))]
    public void Read_returns_invalid_for_unsafe_office_document_target(
        string? target,
        string? targetMode)
    {
        using var stream = new OpenXmlFixture()
            .WithOfficeDocumentRelationshipTarget(target, targetMode)
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_accepts_matching_standard_main_part_structure()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData("/word/Main%20Document.xml", "word/Main%20Document.xml")]
    [InlineData("/word/Main%25Document.xml", "word/Main%25Document.xml")]
    [InlineData("/word/%252e.xml", "word/%252e.xml")]
    [InlineData("/word/%252f.xml", "word/%252f.xml")]
    public void Read_preserves_escaped_main_part_identity(
        string partName,
        string relationshipTarget)
    {
        using var stream = new OpenXmlFixture()
            .WithRecognizedMainOverridePartName(partName)
            .WithOfficeDocumentRelationshipTarget(relationshipTarget)
            .WithMainDocumentEntryName(relationshipTarget)
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_canonicalizes_unreserved_main_override_escape()
    {
        using var stream = new OpenXmlFixture()
            .WithRecognizedMainOverridePartName("/word/%49ndex.xml")
            .WithOfficeDocumentRelationshipTarget("word/Index.xml")
            .WithMainDocumentEntryName("word/Index.xml")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_accepts_later_segment_colon_main_target()
    {
        using var stream = new OpenXmlFixture()
            .WithRecognizedMainOverridePartName("/folder/foo:bar.xml")
            .WithOfficeDocumentRelationshipTarget("folder/foo:bar.xml")
            .WithMainDocumentEntryName("folder/foo:bar.xml")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_resolves_main_part_case_insensitively()
    {
        using var stream = new OpenXmlFixture()
            .WithOfficeDocumentRelationshipTarget("WORD/DOCUMENT.XML")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_case_equivalent_main_part_duplicates()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .AddEntry("WORD/DOCUMENT.XML", "<document />")
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData("Word", false)]
    [InlineData("Excel", false)]
    [InlineData("PowerPoint", false)]
    [InlineData("Word", true)]
    [InlineData("Excel", true)]
    [InlineData("PowerPoint", true)]
    public void Read_supports_strict_office_document_relationships(
        string documentKind,
        bool includeClassification)
    {
        var fixture = new OpenXmlFixture()
            .WithDocumentKind(Enum.Parse<OpenXmlDocumentKind>(documentKind))
            .WithStrictOfficeDocumentRelationship();

        if (!includeClassification)
        {
            fixture.WithoutClassificationRelationships().WithoutLabelInfoEntry();
        }

        using var stream = fixture.Create();
        var result = OpenXmlLabelReader.Read(stream);

        Assert.Equal(
            includeClassification ? LabelStatus.Labeled : LabelStatus.Unlabeled,
            result.Status);
        Assert.Equal(
            includeClassification ? new[] { FirstLabelId } : Array.Empty<Guid>(),
            result.LabelIds);
    }

    [Fact]
    public void Read_returns_invalid_when_main_part_entry_is_missing()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .WithoutMainDocument()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_accepts_case_equivalent_main_part_entry()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .WithMainDocumentEntryName("Word/document.xml")
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_duplicate_exact_main_part_entries()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .AddEntry("word/document.xml", "<document />")
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_without_recognized_main_override()
    {
        using var stream = new OpenXmlFixture()
            .WithoutRecognizedMainOverride()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_main_override_part_name_has_no_leading_slash()
    {
        using var stream = new OpenXmlFixture()
            .WithRecognizedMainOverridePartName("word/document.xml")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_multiple_recognized_main_overrides()
    {
        using var stream = new OpenXmlFixture()
            .AddRecognizedMainOverride("/word/document.xml")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [MemberData(nameof(UnsafeMainOverridePartNames))]
    public void Read_returns_invalid_for_unsafe_main_override_part_name(string partName)
    {
        using var stream = new OpenXmlFixture()
            .WithRecognizedMainOverridePartName(partName)
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_main_override_and_relationship_targets_differ()
    {
        using var stream = new OpenXmlFixture()
            .WithRecognizedMainOverridePartName("/word/other.xml")
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .AddEntry("word/other.xml", "<document />")
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_recognized_office_package_has_no_root_relationships()
    {
        using var stream = new OpenXmlFixture()
            .WithoutRelationships()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_recognizes_macro_enabled_office_main_content_type()
    {
        var contentTypes =
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Override PartName=\"/word/document.xml\" " +
            "ContentType=\"application/vnd.ms-word.document.macroEnabled.main+xml\" />" +
            "</Types>";
        using var stream = new OpenXmlFixture()
            .WithContentTypesXml(contentTypes)
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [MemberData(nameof(UnsafeClassificationTargets))]
    public void Read_returns_invalid_for_unsafe_classification_target(
        string target,
        string? targetMode)
    {
        using var stream = new OpenXmlFixture()
            .WithClassificationRelationshipTarget(target, targetMode)
            .Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_returns_invalid_for_multiple_classification_relationships(bool sameTarget)
    {
        var secondTarget = sameTarget
            ? "docMetadata/LabelInfo.xml"
            : "metadata/second-label.xml";
        var fixture = new OpenXmlFixture()
            .AddClassificationRelationship(secondTarget);

        if (!sameTarget)
        {
            fixture.AddEntry(
                secondTarget,
                OpenXmlFixture.LabelInfo(
                    new LabelInfoValue(SecondLabelId.ToString("D"))));
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_when_classification_target_entry_is_missing()
    {
        using var stream = new OpenXmlFixture().WithoutLabelInfoEntry().Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_malformed_label_info_xml()
    {
        using var stream = new OpenXmlFixture().WithLabelInfoXml("<labelInfo").Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_invalid_for_malformed_custom_properties_xml()
    {
        using var stream = LegacyOnlyFixture().WithCustomPropertiesXml("<Properties").Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData("label")]
    [InlineData("custom")]
    [InlineData("relationships")]
    public void Read_returns_invalid_for_oversized_relevant_metadata(string entryKind)
    {
        var padding = new string(' ', MetadataMaximumBytes + 1);
        var fixture = new OpenXmlFixture();

        switch (entryKind)
        {
            case "label":
                fixture.WithLabelInfoXml(
                    $"<labelInfo xmlns=\"{OpenXmlFixture.LabelNamespace}\">{padding}</labelInfo>");
                break;
            case "custom":
                fixture.WithCustomPropertiesXml(
                    $"<Properties xmlns=\"{CustomPropertiesNamespace}\">{padding}</Properties>");
                break;
            case "relationships":
                fixture.WithRelationshipsXml(
                    $"<Relationships xmlns=\"{RelationshipsNamespace}\">{padding}</Relationships>");
                break;
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Theory]
    [InlineData("content-types")]
    [InlineData("relationships")]
    [InlineData("label")]
    [InlineData("custom")]
    public void Read_returns_invalid_for_duplicate_relevant_entry_names(string entryKind)
    {
        var fixture = new OpenXmlFixture();

        switch (entryKind)
        {
            case "content-types":
                fixture.AddEntry("[Content_Types].xml", OpenXmlFixture.GenericContentTypesXml());
                break;
            case "relationships":
                fixture.AddEntry(
                    "_rels/.rels",
                    $"<Relationships xmlns=\"{RelationshipsNamespace}\" />");
                break;
            case "label":
                fixture.AddEntry(
                    "docMetadata/LabelInfo.xml",
                    OpenXmlFixture.LabelInfo(
                        new LabelInfoValue(SecondLabelId.ToString("D"))));
                break;
            case "custom":
                var customXml = OpenXmlFixture.CustomProperties(
                    new CustomPropertyValue($"MSIP_Label_{SecondLabelId:D}_Enabled", "true"));
                fixture.WithCustomPropertiesXml(customXml).AddEntry("docProps/custom.xml", customXml);
                break;
        }

        using var stream = fixture.Create();

        Assert.Equal(LabelStatus.InvalidFile, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_does_not_search_main_document_body_for_label_properties()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .WithMainDocumentXml(
                $"<document>MSIP_Label_{FirstLabelId:D}_Enabled=true</document>")
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unlabeled_for_valid_office_package_without_active_metadata()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfo(
                new LabelInfoValue(
                    FirstLabelId.ToString("D"),
                    FirstSiteId.ToString("D"),
                    Enabled: "false"))
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    [Fact]
    public void Read_returns_unlabeled_for_valid_office_package_without_label_metadata()
    {
        using var stream = new OpenXmlFixture()
            .WithoutClassificationRelationships()
            .WithoutLabelInfoEntry()
            .Create();

        Assert.Equal(LabelStatus.Unlabeled, OpenXmlLabelReader.Read(stream).Status);
    }

    private static OpenXmlFixture LegacyOnlyFixture() => new OpenXmlFixture()
        .WithoutClassificationRelationships()
        .WithoutLabelInfoEntry();

    private static string LargeGenericContentTypesXml(int paddingCharacters) =>
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        new string(' ', paddingCharacters) +
        "</Types>";

    private static string LargeGenericRelationshipsXml(int paddingCharacters) =>
        $"<Relationships xmlns=\"{RelationshipsNamespace}\">" +
        new string(' ', paddingCharacters) +
        "</Relationships>";

    private static CustomPropertyValue[] LegacyProperties(Guid labelId, Guid siteId) =>
    [
        new CustomPropertyValue($"MSIP_Label_{labelId:D}_Enabled", "true"),
        new CustomPropertyValue($"MSIP_Label_{labelId:D}_SiteId", siteId.ToString("D"))
    ];
}
