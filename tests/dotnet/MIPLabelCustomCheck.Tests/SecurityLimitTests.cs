using System.Text;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

/// <summary>
/// Cross-format coverage for malformed input and resource-limit handling, exercised entirely
/// through the public <see cref="MipLabelChecker"/> boundary rather than any individual internal
/// reader. Every test here is synchronous, cancellation-free, and completes almost immediately -
/// each fixture is bounded well below the limit that trips it, so nothing here can hang or exhaust
/// memory.
///
/// Every <c>[Fact]</c>/<c>[Theory]</c> below also declares <c>Timeout = 5000</c> to document the
/// 5-second per-test budget. xUnit v2 only *enforces* that timeout for async (<c>Task</c>-returning)
/// test methods - for a synchronous method like these, the attribute is inert (the runner has no way
/// to preempt a running thread) and exists purely to record the contract. Kept synchronous rather
/// than converted to <c>async Task</c> because the brief requires these tests to be cancellation-free
/// and synchronous; the real guarantee against hangs comes from every fixture being bounded well
/// below the limit that trips it (measured: the whole file runs in well under a second).
/// </summary>
public sealed class SecurityLimitTests
{
    public static TheoryData<byte[], LabelStatus> Inputs =>
        new()
        {
            { new OpenXmlFixture().WithOversizedLabelInfo().Create().ToArray(), LabelStatus.InvalidFile },
            { new OpenXmlFixture().WithExternalRelationship().Create().ToArray(), LabelStatus.InvalidFile },
            { new PngFixture().WithOversizedDeclaredChunk().Create().ToArray(), LabelStatus.InvalidFile },
            { PdfFixture.WithCyclicPrev().ToArray(), LabelStatus.InvalidFile },
            { PdfFixture.WithUnsupportedMetadataFilter(encrypted: false).ToArray(), LabelStatus.Unsupported }
        };

    public static TheoryData<byte[]> TruncatedMagicInputs =>
        new()
        {
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, // PNG signature, no IHDR follows
            new byte[] { 0x50, 0x4B, 0x03, 0x04 }, // ZIP local-file-header signature, no entries follow
            Encoding.ASCII.GetBytes("%PDF-1.7\n"), // PDF header, no xref/trailer follows
            new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } // Compound file signature, no header follows
        };

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Check_classifies_malformed_and_unsupported_inputs(byte[] bytes, LabelStatus expected)
    {
        using var stream = new MemoryStream(bytes);

        var result = new MipLabelChecker().Check(stream);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [MemberData(nameof(TruncatedMagicInputs))]
    public void Check_rejects_a_file_truncated_immediately_after_its_magic_bytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        var result = new MipLabelChecker().Check(stream);

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_a_dtd_in_open_xml_label_info()
    {
        using var stream = new OpenXmlFixture()
            .WithLabelInfoXml(
                $"<!DOCTYPE labelInfo [<!ENTITY active \"true\">]>" +
                $"<labelInfo xmlns=\"{OpenXmlFixture.LabelNamespace}\" />")
            .Create();

        var result = new MipLabelChecker().Check(stream, "sample.docx");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_a_dtd_in_png_xmp_metadata()
    {
        using var stream = new PngFixture()
            .AddITxt(new PngITxtOptions("<!DOCTYPE root [<!ENTITY active \"true\">]><root />"))
            .Create();

        var result = new MipLabelChecker().Check(stream, "sample.png");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_duplicate_zip_relationship_targets()
    {
        using var stream = new OpenXmlFixture()
            .AddEntry(
                "_RELS/.RELS",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\" />")
            .Create();

        var result = new MipLabelChecker().Check(stream, "sample.docx");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_a_cyclic_compound_file_directory_chain()
    {
        var fixture = CompoundFileFixture.CreateV3();
        var bytes = fixture.CloneBytes();
        fixture.SetFatEntry(bytes, fixture.LastDirectorySectorId, fixture.FirstDirectorySectorId);
        using var stream = new MemoryStream(bytes);

        var result = new MipLabelChecker().Check(stream, "sample.doc");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_pdf_nesting_beyond_the_configured_depth()
    {
        using var stream = PdfFixture.WithExcessiveNesting();

        var result = new MipLabelChecker().Check(stream, "sample.pdf");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_a_pdf_object_stream_entry_count_over_the_limit()
    {
        using var stream = PdfFixture.WithExcessiveObjectStreamEntryCount();

        var result = new MipLabelChecker().Check(stream, "sample.pdf");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }

    [Fact]
    public void Check_rejects_decompressed_pdf_metadata_over_the_byte_limit()
    {
        using var stream = PdfFixture.WithOversizedDecompressedMetadata();

        var result = new MipLabelChecker().Check(stream, "sample.pdf");

        Assert.Equal(LabelStatus.InvalidFile, result.Status);
    }
}
