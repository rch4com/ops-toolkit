using System.Text;
using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class LabelInfoXmlReaderTests
{
    private const string LabelNamespace = "http://schemas.microsoft.com/office/2020/mipLabelMetadata";
    private static readonly Guid FirstId = Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");
    private static readonly Guid SecondId = Guid.Parse("e19cdb69-0515-4f99-a455-60417f2e080d");
    private static readonly Guid FirstSiteId = Guid.Parse("44b11702-0369-4898-baf3-fdbfd76afc2f");
    private static readonly Guid SecondSiteId = Guid.Parse("2072793a-2c67-49bf-bf25-42092d64b0cb");

    [Fact]
    public void Read_parses_active_label_and_defaults_missing_removed_to_false()
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label id="{{FirstId:D}}" enabled="TRUE" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Equal(
            new[] { new LabelInfoRecord(FirstId, null, Enabled: true, Removed: false) },
            result.Records);
    }

    [Fact]
    public void Read_preserves_disabled_and_removed_records_with_site_ids()
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label id="{{FirstId:D}}" siteId="{{FirstSiteId:D}}" enabled="0" removed="false" />
              <label id="{{SecondId:D}}" siteId="{{SecondSiteId:D}}" enabled="true" removed="1" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Equal(
            new[]
            {
                new LabelInfoRecord(FirstId, FirstSiteId, Enabled: false, Removed: false),
                new LabelInfoRecord(SecondId, SecondSiteId, Enabled: true, Removed: true)
            },
            result.Records);
    }

    [Fact]
    public void Read_defaults_missing_enabled_and_removed_to_false()
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label id="{{FirstId:D}}" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.Equal(
            new[] { new LabelInfoRecord(FirstId, null, Enabled: false, Removed: false) },
            result.Records);
    }

    [Fact]
    public void Read_ignores_label_local_name_in_wrong_namespace()
    {
        var xml = $$"""
            <root xmlns:mip="{{LabelNamespace}}" xmlns:other="urn:not-mip">
              <other:label id="{{FirstId:D}}" enabled="true" />
              <mip:label id="{{SecondId:D}}" enabled="true" />
            </root>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Equal(new[] { SecondId }, result.Records.Select(record => record.LabelId));
    }

    [Fact]
    public void Read_accepts_guids_with_braces()
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label id="{{FirstId:B}}" siteId="{{FirstSiteId:B}}" enabled="1" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Equal(FirstId, Assert.Single(result.Records).LabelId);
        Assert.Equal(FirstSiteId, result.Records[0].SiteId);
    }

    [Fact]
    public void Read_marks_missing_or_malformed_required_id_invalid()
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label enabled="true" />
              <label id="not-a-guid" enabled="true" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.True(result.InvalidMetadata);
    }

    [Fact]
    public void Read_marks_malformed_present_site_id_invalid()
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label id="{{FirstId:D}}" siteId="not-a-guid" enabled="true" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.True(result.InvalidMetadata);
    }

    [Theory]
    [InlineData("enabled")]
    [InlineData("removed")]
    public void Read_marks_unrecognized_present_boolean_invalid(string attributeName)
    {
        var xml = $$"""
            <labelInfo xmlns="{{LabelNamespace}}">
              <label id="{{FirstId:D}}" {{attributeName}}="yes" />
            </labelInfo>
            """;
        using var stream = CreateStream(xml);

        var result = LabelInfoXmlReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.True(result.InvalidMetadata);
    }

    private static MemoryStream CreateStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
