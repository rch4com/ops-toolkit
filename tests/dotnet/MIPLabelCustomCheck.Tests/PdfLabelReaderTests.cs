using MIPLabelCustomCheck.Internal;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class PdfLabelReaderTests
{
    private static readonly Guid LabelId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Read_detects_real_pdf_and_deduplicates_info_and_xmp()
    {
        using var stream = File.OpenRead(FixturePaths.Get(".pdf"));
        var result = PdfLabelReader.Read(stream);
        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a"), Assert.Single(result.LabelIds));
        Assert.Equal(ProtectionType.None, result.Protection);
    }

    [Fact]
    public void Read_finds_a_label_from_info_only()
    {
        using var stream = PdfFixture.WithInfoLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([LabelId], result.LabelIds);
        Assert.Equal(ProtectionType.None, result.Protection);
    }

    [Fact]
    public void Read_finds_a_label_from_metadata_xmp_only()
    {
        using var stream = PdfFixture.WithMetadataLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([LabelId], result.LabelIds);
        Assert.Equal(ProtectionType.None, result.Protection);
    }

    [Fact]
    public void Read_deduplicates_a_label_present_in_both_info_and_metadata_xmp()
    {
        using var stream = PdfFixture.WithDuplicateInfoAndMetadataLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal(LabelId, Assert.Single(result.LabelIds));
    }

    [Fact]
    public void Read_reports_labeled_with_native_protection_when_encrypted_and_a_label_is_readable()
    {
        using var stream = PdfFixture.WithEncryptedInfoLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([LabelId], result.LabelIds);
        Assert.Equal(ProtectionType.Native, result.Protection);
    }

    [Fact]
    public void Read_reports_protected_unknown_when_encrypted_without_a_readable_label()
    {
        using var stream = PdfFixture.WithEncryptedNoLabel();

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_reports_encrypt_filter_and_subfilter_protection_detail()
    {
        using var stream = PdfFixture.WithEncryptionFilterAndSubFilter();

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
        Assert.Equal("PDF /Encrypt /Filter /Standard /SubFilter /adbe.pkcs7.s5", result.ProtectionDetail);
    }

    [Fact]
    public void Read_tolerates_an_unsupported_metadata_filter_when_encrypted()
    {
        using var stream = PdfFixture.WithUnsupportedMetadataFilter(encrypted: true);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_reports_protected_unknown_when_encrypted_metadata_is_ciphertext_that_fails_to_inflate()
    {
        using var stream = PdfFixture.WithEncryptedCiphertextMetadata();

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.ProtectedLabelUnknown, result.Status);
        Assert.Equal(ProtectionType.Native, result.Protection);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_reports_invalid_file_for_a_dangling_metadata_reference_even_when_encrypted()
    {
        using var stream = PdfFixture.WithEncryptedDanglingMetadataReference();

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Read_reports_invalid_file_for_a_malformed_enabled_label_guid()
    {
        using var stream = PdfFixture.WithMalformedInfoLabel();

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Read_reports_unsupported_for_an_unsupported_metadata_filter()
    {
        using var stream = PdfFixture.WithUnsupportedMetadataFilter(encrypted: false);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Unsupported, result.Status);
    }

    [Fact]
    public void Read_reports_unlabeled_when_there_is_no_label_and_no_encryption()
    {
        using var stream = PdfFixture.WithClassicXref();

        var result = PdfLabelReader.Read(stream);

        Assert.Equal(LabelStatus.Unlabeled, result.Status);
        Assert.Equal(ProtectionType.None, result.Protection);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_reports_pdf_info_source_for_info_only_label()
    {
        using var stream = PdfFixture.WithInfoLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal([LabelSource.PdfInfoDictionary], result.Sources);
    }

    [Fact]
    public void Read_reports_xmp_source_for_metadata_only_label()
    {
        using var stream = PdfFixture.WithMetadataLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal([LabelSource.Xmp], result.Sources);
    }

    [Fact]
    public void Read_reports_first_seen_source_for_duplicate_info_and_metadata_label()
    {
        using var stream = PdfFixture.WithDuplicateInfoAndMetadataLabel(LabelId);

        var result = PdfLabelReader.Read(stream);

        Assert.Equal([LabelSource.PdfInfoDictionary], result.Sources);
    }
}
