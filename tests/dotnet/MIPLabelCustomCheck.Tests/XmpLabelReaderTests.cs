using System.Text;
using System.Xml;
using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class XmpLabelReaderTests
{
    private static readonly Guid LabelId = Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");

    [Fact]
    public void Read_finds_rdf_attribute_by_local_name()
    {
        var xml = CreateAttributeXml($"MSIP_Label_{LabelId:D}_Enabled", "true");
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Equal(new[] { LabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_finds_leaf_element_by_local_name()
    {
        var xml = CreateElementXml($"MSIP_Label_{LabelId:D}_Enabled", "TRUE");
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Equal(new[] { LabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_deduplicates_attribute_and_element_metadata()
    {
        var propertyName = $"MSIP_Label_{LabelId:D}_Enabled";
        var xml = $$"""
            <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:any-msip-uri">
              <rdf:Description msip:{{propertyName}}="true">
                <msip:{{propertyName}}>1</msip:{{propertyName}}>
              </rdf:Description>
            </rdf:RDF>
            """;
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.Equal(new[] { LabelId }, result.LabelIds);
    }

    [Fact]
    public void Read_ignores_inactive_property()
    {
        var xml = CreateAttributeXml($"MSIP_Label_{LabelId:D}_Enabled", "false");
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_marks_malformed_active_property_key_invalid()
    {
        var xml = CreateElementXml("MSIP_Label_not-a-guid_Enabled", "1");
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.True(result.InvalidMetadata);
    }

    [Fact]
    public void Read_ignores_namespace_declaration_named_like_valid_property()
    {
        var xml = $$"""
            <root xmlns:MSIP_Label_{{LabelId:D}}_Enabled="true" />
            """;
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_ignores_namespace_declaration_named_like_malformed_property()
    {
        const string xml = """
            <root xmlns:MSIP_Label_not-a-guid_Enabled="true" />
            """;
        using var stream = CreateStream(xml);

        var result = XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml));

        Assert.False(result.InvalidMetadata);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void Read_rejects_dtd_and_leaves_caller_stream_open()
    {
        var xml = $$"""
            <!DOCTYPE root [<!ENTITY active "true">]>
            <root xmlns:msip="urn:any-msip-uri">
              <msip:MSIP_Label_{{LabelId:D}}_Enabled>&active;</msip:MSIP_Label_{{LabelId:D}}_Enabled>
            </root>
            """;
        var stream = CreateStream(xml);

        Assert.Throws<XmlException>(() =>
            XmpLabelReader.Read(stream, Encoding.UTF8.GetByteCount(xml)));
        Assert.True(stream.CanRead);

        stream.Dispose();
    }

    [Fact]
    public void Read_accepts_exact_size_limit_and_leaves_caller_stream_open()
    {
        var xml = CreateElementXml($"MSIP_Label_{LabelId:D}_Enabled", "true");
        var bytes = Encoding.UTF8.GetBytes(xml);
        var stream = new MemoryStream(bytes);

        var result = XmpLabelReader.Read(stream, bytes.Length);

        Assert.Equal(new[] { LabelId }, result.LabelIds);
        Assert.True(stream.CanRead);

        stream.Dispose();
    }

    [Fact]
    public void Read_rejects_input_over_size_limit_and_leaves_caller_stream_open()
    {
        var xml = CreateElementXml($"MSIP_Label_{LabelId:D}_Enabled", "true");
        var bytes = Encoding.UTF8.GetBytes(xml);
        var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(() => XmpLabelReader.Read(stream, bytes.Length - 1));
        Assert.True(stream.CanRead);

        stream.Dispose();
    }

    private static string CreateAttributeXml(string propertyName, string value) => $$"""
        <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:any-msip-uri">
          <rdf:Description msip:{{propertyName}}="{{value}}" />
        </rdf:RDF>
        """;

    private static string CreateElementXml(string propertyName, string value) => $$"""
        <rdf:RDF xmlns:rdf="urn:rdf" xmlns:msip="urn:any-msip-uri">
          <rdf:Description>
            <msip:{{propertyName}}>{{value}}</msip:{{propertyName}}>
          </rdf:Description>
        </rdf:RDF>
        """;

    private static MemoryStream CreateStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
