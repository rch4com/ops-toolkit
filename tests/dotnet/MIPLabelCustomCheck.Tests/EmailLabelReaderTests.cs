using System.Text;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class EmailLabelReaderTests
{
    private static readonly Guid LabelId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid SiteId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Check_eml_reads_msip_labels_header()
    {
        var message = "From: sender@example.test\r\n" +
            "MSIP_Labels: " +
            $"MSIP_Label_{LabelId}_Enabled=true; " +
            $"MSIP_Label_{LabelId}_SiteId={SiteId}; " +
            $"MSIP_Label_{LabelId}_Name=Confidential\r\n" +
            "\r\nbody";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(message));

        var result = new MipLabelChecker().Check(stream, "message.eml", LabelCheckOptions.WithDetails);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([LabelId], result.LabelIds);
        Assert.Equal([LabelSource.EmailHeader], result.Sources);
        var detail = Assert.Single(result.Details);
        Assert.Equal(SiteId, detail.SiteId);
        Assert.Equal("Confidential", detail.Name);
        Assert.Equal(ProtectionType.None, result.Protection);
    }

    [Fact]
    public void Check_eml_reads_direct_msip_label_header_conservatively()
    {
        var message = $"MSIP_Label_{LabelId}_Enabled: true\r\n\r\nbody";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(message));

        var result = new MipLabelChecker().Check(stream, "message.eml");

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([LabelId], result.LabelIds);
        Assert.Equal([LabelSource.EmailHeader], result.Sources);
        Assert.Equal(ProtectionType.None, result.Protection);
    }

    [Fact]
    public void Check_msg_reads_public_string_named_msip_properties()
    {
        var bytes = MsgFixture.CreateWithPublicStringProperties(
            new Dictionary<string, string>
            {
                [$"MSIP_Label_{LabelId}_Enabled"] = "true",
                [$"MSIP_Label_{LabelId}_SiteId"] = SiteId.ToString(),
                [$"MSIP_Label_{LabelId}_Name"] = "Confidential"
            });
        using var stream = new MemoryStream(bytes);

        var result = new MipLabelChecker().Check(stream, "message.msg", LabelCheckOptions.WithDetails);

        Assert.Equal(LabelStatus.Labeled, result.Status);
        Assert.Equal([LabelId], result.LabelIds);
        Assert.Equal([LabelSource.MsgNamedProperty], result.Sources);
        var detail = Assert.Single(result.Details);
        Assert.Equal(SiteId, detail.SiteId);
        Assert.Equal("Confidential", detail.Name);
        Assert.Equal(ProtectionType.None, result.Protection);
    }
}
