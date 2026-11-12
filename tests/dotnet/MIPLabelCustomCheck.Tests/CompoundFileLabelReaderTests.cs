using System.Text;
using MIPLabelCustomCheck.Internal;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class CompoundFileLabelReaderTests
{
    private static readonly string DocumentSummaryInformationPath =
        ((char)5) + "DocumentSummaryInformation";

    private static readonly string DataSpacesPath = ((char)6) + "DataSpaces";

    private static readonly string LabelInfoPath = DataSpacesPath + "/TransformInfo/LabelInfo";

    private static readonly string IrmTransformPath =
        DataSpacesPath + "/TransformInfo/Microsoft.Metadata.IRMProtector";

    private const string EncryptedPackagePath = "EncryptedPackage";

    [Theory]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    public void Read_detects_real_legacy_office_label(string extension)
    {
        using var stream = File.OpenRead(FixturePaths.Get(extension));
        var result = CompoundFileLabelReader.Read(stream);
        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(
            Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a"),
            Assert.Single(result.LabelIds));
    }

    [Fact]
    public void LabelInfo_tenant_exclusion_wins_over_a_custom_property_from_the_same_site()
    {
        var labelInfoGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var customGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var sharedSiteId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var view = new FakeCompoundFileView()
            .AddStorage(DataSpacesPath)
            .AddStream(
                LabelInfoPath,
                BuildLabelInfoXml((labelInfoGuid, sharedSiteId, Enabled: true, Removed: false)))
            .AddStream(
                DocumentSummaryInformationPath,
                BuildCustomProperties((customGuid, sharedSiteId, "true")));

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([labelInfoGuid], result.LabelIds);
    }

    [Fact]
    public void Different_tenant_custom_properties_still_merge_alongside_LabelInfo()
    {
        var labelInfoGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var labelInfoSiteId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var customGuid = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var otherSiteId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var view = new FakeCompoundFileView()
            .AddStorage(DataSpacesPath)
            .AddStream(
                LabelInfoPath,
                BuildLabelInfoXml((labelInfoGuid, labelInfoSiteId, Enabled: true, Removed: false)))
            .AddStream(
                DocumentSummaryInformationPath,
                BuildCustomProperties((customGuid, otherSiteId, "true")));

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(
            new HashSet<Guid> { labelInfoGuid, customGuid },
            result.LabelIds.ToHashSet());
    }

    [Fact]
    public void Removed_LabelInfo_record_suppresses_the_same_tenants_custom_property()
    {
        var labelInfoGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var customGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var sharedSiteId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var view = new FakeCompoundFileView()
            .AddStorage(DataSpacesPath)
            .AddStream(
                LabelInfoPath,
                BuildLabelInfoXml((labelInfoGuid, sharedSiteId, Enabled: true, Removed: true)))
            .AddStream(
                DocumentSummaryInformationPath,
                BuildCustomProperties((customGuid, sharedSiteId, "true")));

        var result = CompoundFileLabelReader.Read(view);

        Assert.DoesNotContain(customGuid, result.LabelIds);
        Assert.Empty(result.LabelIds);
        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
    }

    [Fact]
    public void Protected_encrypted_package_without_a_readable_guid_is_protected_unknown()
    {
        var view = new FakeCompoundFileView()
            .AddStream(EncryptedPackagePath, [1, 2, 3]);

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
        Assert.Empty(result.LabelIds);
    }

    [Theory]
    [InlineData("DataSpaces", "DataSpaces")]
    [InlineData("EncryptedPackage", "EncryptedPackage")]
    [InlineData("IrmTransform", "IRM transform")]
    public void Protected_cfb_signal_reports_protection_detail(string signal, string expectedDetail)
    {
        var view = signal switch
        {
            "DataSpaces" => new FakeCompoundFileView().AddStorage(DataSpacesPath),
            "EncryptedPackage" => new FakeCompoundFileView().AddStream(EncryptedPackagePath, [1]),
            "IrmTransform" => new FakeCompoundFileView().AddStorage(IrmTransformPath),
            _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, null)
        };

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
        Assert.Equal(expectedDetail, result.ProtectionDetail);
    }

    [Fact]
    public void No_metadata_at_all_is_unlabeled()
    {
        var view = new FakeCompoundFileView();

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.Unlabeled, result.Status);
        Assert.Equal(ProtectionType.None, result.Protection);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Custom_properties_alone_without_protection_markers_are_labeled_with_no_protection()
    {
        var labelGuid = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var view = new FakeCompoundFileView()
            .AddStream(
                DocumentSummaryInformationPath,
                BuildCustomProperties((labelGuid, SiteId: null, "true")));

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(ProtectionType.None, result.Protection);
        Assert.Equal([labelGuid], result.LabelIds);
    }

    [Fact]
    public void A_non_ascii_custom_property_under_an_unsupported_code_page_does_not_hide_a_genuine_label()
    {
        // Regression test: a legacy file's DocumentSummaryInformation section can declare an
        // unsupported code page (e.g. 949, Korean) while containing both a genuine ASCII MSIP
        // label property and an unrelated non-ASCII custom property (e.g. Korean
        // records-management metadata) under that same code page. The unreadable property
        // must be skipped on its own; it must not cause the whole section - and therefore the
        // real label - to be discarded as "no properties".
        var labelGuid = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        const int codePage = 949;

        var properties = OlePropertySetBuilder.Build(
            codePage,
            [
                new OlePropertySetBuilder.Prop(
                    2,
                    $"MSIP_Label_{labelGuid}_Enabled",
                    OlePropertySetBuilder.TypedString("true", codePage)),
                new OlePropertySetBuilder.Prop(
                    3,
                    "OtherMetadata",
                    OlePropertySetBuilder.TypedStringFromRawBytes([0x80, 0x00])),
            ]);

        var view = new FakeCompoundFileView()
            .AddStream(DocumentSummaryInformationPath, properties);

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([labelGuid], result.LabelIds);
    }

    [Fact]
    public void Invalid_LabelInfo_metadata_is_reported_as_invalid_file()
    {
        var view = new FakeCompoundFileView()
            .AddStorage(DataSpacesPath)
            .AddStream(LabelInfoPath, "not xml"u8.ToArray());

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Malformed_custom_site_id_for_an_active_label_is_reported_as_invalid_file()
    {
        var labelGuid = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        const int codePage = 1252;

        var properties = OlePropertySetBuilder.Build(
            codePage,
            [
                new OlePropertySetBuilder.Prop(
                    2,
                    $"MSIP_Label_{labelGuid}_Enabled",
                    OlePropertySetBuilder.TypedString("true", codePage)),
                new OlePropertySetBuilder.Prop(
                    3,
                    $"MSIP_Label_{labelGuid}_SiteId",
                    OlePropertySetBuilder.TypedString("not-a-guid", codePage)),
            ]);

        var view = new FakeCompoundFileView()
            .AddStream(DocumentSummaryInformationPath, properties);

        var result = CompoundFileLabelReader.Read(view);

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    private static byte[] BuildLabelInfoXml(
        params (Guid LabelId, Guid? SiteId, bool Enabled, bool Removed)[] records)
    {
        var builder = new StringBuilder();
        builder.Append("<labelList xmlns=\"http://schemas.microsoft.com/office/2020/mipLabelMetadata\">");
        foreach (var record in records)
        {
            builder.Append("<label id=\"").Append(record.LabelId).Append('"');
            if (record.SiteId.HasValue)
            {
                builder.Append(" siteId=\"").Append(record.SiteId.Value).Append('"');
            }

            builder.Append(" enabled=\"").Append(record.Enabled ? '1' : '0').Append('"');
            builder.Append(" removed=\"").Append(record.Removed ? '1' : '0').Append('"');
            builder.Append("/>");
        }

        builder.Append("</labelList>");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] BuildCustomProperties(
        params (Guid LabelId, Guid? SiteId, string EnabledValue)[] groups)
    {
        const int codePage = 1252;
        var properties = new List<OlePropertySetBuilder.Prop>();
        uint id = 2;
        foreach (var group in groups)
        {
            properties.Add(new OlePropertySetBuilder.Prop(
                id++,
                $"MSIP_Label_{group.LabelId}_Enabled",
                OlePropertySetBuilder.TypedString(group.EnabledValue, codePage)));

            if (group.SiteId.HasValue)
            {
                properties.Add(new OlePropertySetBuilder.Prop(
                    id++,
                    $"MSIP_Label_{group.LabelId}_SiteId",
                    OlePropertySetBuilder.TypedString(group.SiteId.Value.ToString(), codePage)));
            }
        }

        return OlePropertySetBuilder.Build(codePage, properties);
    }
}
