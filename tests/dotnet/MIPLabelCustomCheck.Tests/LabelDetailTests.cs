namespace MIPLabelCustomCheck.Tests;

/// <summary>
/// Covers <see cref="LabelCheckOptions.IncludeDetails"/> against the repository's real samples.
/// The expected values were read out of the files themselves, not assumed.
/// </summary>
public sealed class LabelDetailTests
{
    private static readonly Guid OfficeLabelId = Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");
    private static readonly Guid PngLabelId = Guid.Parse("e1bdcc70-4bee-4888-a93a-4203339b523a");
    private static readonly Guid TenantId = Guid.Parse("60bf846f-b960-4abf-b752-5086ae3870a8");

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    [InlineData(".pdf")]
    [InlineData(".png")]
    public void Details_are_empty_when_not_requested(string extension)
    {
        var result = new MipLabelChecker().Check(FixturePaths.Get(extension));

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Empty(result.Details);
    }

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    [InlineData(".pdf")]
    [InlineData(".png")]
    public void Details_carry_one_entry_per_label_when_requested(string extension)
    {
        var result = new MipLabelChecker().Check(
            FixturePaths.Get(extension), LabelCheckOptions.WithDetails);

        Assert.Equal(result.LabelIds.Count, result.Details.Count);
        Assert.Equal(result.LabelIds, result.Details.Select(detail => detail.Id));
    }

    /// <summary>Every real sample was labelled by the same tenant, whatever the format.</summary>
    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    [InlineData(".pdf")]
    [InlineData(".png")]
    public void Details_report_the_tenant_and_an_explicit_apply_method(string extension)
    {
        var result = new MipLabelChecker().Check(
            FixturePaths.Get(extension), LabelCheckOptions.WithDetails);

        var detail = Assert.Single(result.Details);
        Assert.Equal(TenantId, detail.SiteId);
        Assert.Equal(LabelMethod.Privileged, detail.Method);
        Assert.Equal(0, detail.ContentBits);
    }

    [Fact]
    public void Ooxml_details_come_from_the_label_info_part()
    {
        var result = new MipLabelChecker().Check(
            FixturePaths.Get(".docx"), LabelCheckOptions.WithDetails);

        var detail = Assert.Single(result.Details);
        Assert.Equal(OfficeLabelId, detail.Id);
        Assert.Equal(TenantId, detail.SiteId);
        Assert.Equal(LabelMethod.Privileged, detail.Method);
        Assert.Equal(0, detail.ContentBits);
    }

    /// <summary>
    /// The legacy Office samples carry the fullest metadata of the eight: unlike the OOXML
    /// LabelInfo part, their MSIP properties include a name, a set date, and an action id.
    /// </summary>
    [Fact]
    public void Legacy_office_details_include_name_set_date_and_action_id()
    {
        var result = new MipLabelChecker().Check(
            FixturePaths.Get(".doc"), LabelCheckOptions.WithDetails);

        var detail = Assert.Single(result.Details);
        Assert.Equal(OfficeLabelId, detail.Id);
        Assert.NotNull(detail.Name);
        Assert.NotNull(detail.SetDate);
        Assert.NotNull(detail.ActionId);
    }

    /// <summary>The PNG's XMP is the one sample where every documented attribute is present.</summary>
    [Fact]
    public void Png_details_are_read_from_xmp()
    {
        var result = new MipLabelChecker().Check(
            FixturePaths.Get(".png"), LabelCheckOptions.WithDetails);

        var detail = Assert.Single(result.Details);
        Assert.Equal(PngLabelId, detail.Id);
        Assert.Equal(TenantId, detail.SiteId);
        Assert.Equal("Secret_20240717", detail.Name);
        Assert.Equal(LabelMethod.Privileged, detail.Method);
        Assert.Equal(0, detail.ContentBits);
        Assert.Equal(Guid.Parse("f1ff122b-0f6c-4b2c-bf2e-3cc760aa24e3"), detail.ActionId);
        Assert.Equal(
            new DateTimeOffset(2026, 7, 13, 8, 35, 2, TimeSpan.Zero),
            detail.SetDate);
    }

    /// <summary>
    /// The PDF's Info dictionary carries only three of the attributes. The absent ones must read
    /// as null rather than as a default that looks like a real value.
    /// </summary>
    [Fact]
    public void Pdf_details_leave_absent_attributes_null()
    {
        var result = new MipLabelChecker().Check(
            FixturePaths.Get(".pdf"), LabelCheckOptions.WithDetails);

        var detail = Assert.Single(result.Details);
        Assert.Equal(TenantId, detail.SiteId);
        Assert.Equal(LabelMethod.Privileged, detail.Method);
        Assert.Null(detail.Name);
        Assert.Null(detail.SetDate);
        Assert.Null(detail.ActionId);
    }

    [Fact]
    public void Requesting_details_does_not_change_the_label_ids()
    {
        var checker = new MipLabelChecker();

        var plain = checker.Check(FixturePaths.Get(".png"));
        var detailed = checker.Check(FixturePaths.Get(".png"), LabelCheckOptions.WithDetails);

        Assert.Equal(plain.Status, detailed.Status);
        Assert.Equal(plain.LabelIds, detailed.LabelIds);
        Assert.Equal(plain.Protection, detailed.Protection);
    }

    [Fact]
    public void Unlabeled_file_has_no_details_even_when_requested()
    {
        var result = new MipLabelChecker().Check(
            new MemoryStream("not a supported format"u8.ToArray()),
            options: LabelCheckOptions.WithDetails);

        Assert.Equal(LabelStatus.Unsupported, result.Status);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void Default_options_and_no_options_agree()
    {
        var checker = new MipLabelChecker();

        Assert.Equal(
            checker.Check(FixturePaths.Get(".docx")),
            checker.Check(FixturePaths.Get(".docx"), LabelCheckOptions.Default));
    }
}
