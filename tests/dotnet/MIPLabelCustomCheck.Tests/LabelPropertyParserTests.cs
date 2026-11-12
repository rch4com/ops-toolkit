using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck.Tests;

public sealed class LabelPropertyParserTests
{
    [Fact]
    public void ParseProperties_keeps_only_enabled_valid_unique_guids()
    {
        var id = Guid.Parse("6333fd3f-635c-44a8-824c-7f4faab3944a");
        var properties = new[]
        {
            KeyValuePair.Create($"MSIP_Label_{id:D}_Enabled", "true"),
            KeyValuePair.Create($"MSIP_Label_{id:D}_Enabled", "1"),
            KeyValuePair.Create("MSIP_Label_aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa_Enabled", "false")
        };

        var result = LabelPropertyParser.Parse(properties);

        Assert.False(result.InvalidMetadata);
        Assert.Equal(new[] { id }, result.LabelIds);
    }

    [Fact]
    public void ParseProperties_marks_active_key_with_bad_guid_invalid()
    {
        var result = LabelPropertyParser.Parse(
            new[] { KeyValuePair.Create("MSIP_Label_not-a-guid_Enabled", "true") });

        Assert.True(result.InvalidMetadata);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void ParseProperties_accepts_supported_active_values(string value)
    {
        var id = Guid.Parse("6935f3f4-7b3d-4b4e-a80b-805881a805b0");

        var result = LabelPropertyParser.Parse(
            new[] { KeyValuePair.Create($"MSIP_Label_{id:D}_Enabled", value) });

        Assert.False(result.InvalidMetadata);
        Assert.Equal(new[] { id }, result.LabelIds);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("enabled")]
    public void ParseProperties_ignores_inactive_values(string value)
    {
        var id = Guid.Parse("6935f3f4-7b3d-4b4e-a80b-805881a805b0");

        var result = LabelPropertyParser.Parse(
            new[] { KeyValuePair.Create($"MSIP_Label_{id:D}_Enabled", value) });

        Assert.False(result.InvalidMetadata);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void ParseProperties_matches_prefix_and_suffix_ordinally()
    {
        var id = Guid.Parse("6935f3f4-7b3d-4b4e-a80b-805881a805b0");
        var properties = new[]
        {
            KeyValuePair.Create($"msip_Label_{id:D}_Enabled", "true"),
            KeyValuePair.Create($"MSIP_Label_{id:D}_enabled", "true"),
            KeyValuePair.Create($"xMSIP_Label_{id:D}_Enabled", "true"),
            KeyValuePair.Create($"MSIP_Label_{id:D}_Enabledx", "true")
        };

        var result = LabelPropertyParser.Parse(properties);

        Assert.False(result.InvalidMetadata);
        Assert.Empty(result.LabelIds);
    }

    [Fact]
    public void ParseProperties_preserves_first_active_appearance()
    {
        var first = Guid.Parse("ef826966-6b2a-42c7-8362-ffb7c628d8f0");
        var second = Guid.Parse("30167520-f31c-4d24-97f8-f49f9a8983a0");
        var properties = new[]
        {
            KeyValuePair.Create($"MSIP_Label_{second:D}_Enabled", "true"),
            KeyValuePair.Create($"MSIP_Label_{first:D}_Enabled", "1"),
            KeyValuePair.Create($"MSIP_Label_{second:B}_Enabled", "TRUE")
        };

        var result = LabelPropertyParser.Parse(properties);

        Assert.Equal(new[] { second, first }, result.LabelIds);
    }

    [Fact]
    public void ParseProperties_ignores_inactive_key_with_bad_guid()
    {
        var result = LabelPropertyParser.Parse(
            new[] { KeyValuePair.Create("MSIP_Label_not-a-guid_Enabled", "false") });

        Assert.False(result.InvalidMetadata);
        Assert.Empty(result.LabelIds);
    }
}
