using System.Text;
using MIPLabelCustomCheck.Internal.CompoundFile;

namespace MIPLabelCustomCheck.Tests;

public sealed class OlePropertySetReaderTests
{
    [Fact]
    public void Rejects_bad_byte_order()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "MSIP_Label_x_Enabled", OlePropertySetBuilder.TypedString("true", 1252))]);
        bytes[0] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Returns_not_present_when_fmtid_is_missing()
    {
        var bytes = OlePropertySetBuilder.BuildWithoutTargetSection();

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.NotPresent, result.Status);
        Assert.Empty(result.Properties);
    }

    [Fact]
    public void Selects_target_fmtid_section_when_two_sections_present()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "MSIP_Label_guid_Enabled", OlePropertySetBuilder.TypedString("true", 1252))],
            includeOtherSection: true);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        Assert.Contains(
            new KeyValuePair<string, string>("MSIP_Label_guid_Enabled", "true"),
            result.Properties);
    }

    [Fact]
    public void Decodes_vt_i2_i4_bool_and_filetime()
    {
        var fileTime = new DateTime(2026, 7, 13, 8, 16, 25, DateTimeKind.Utc);
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [
                new OlePropertySetBuilder.Prop(2, "ShortValue", OlePropertySetBuilder.TypedI2(-7)),
                new OlePropertySetBuilder.Prop(3, "LongValue", OlePropertySetBuilder.TypedI4(123456)),
                new OlePropertySetBuilder.Prop(4, "BoolTrue", OlePropertySetBuilder.TypedBool(true)),
                new OlePropertySetBuilder.Prop(5, "BoolFalse", OlePropertySetBuilder.TypedBool(false)),
                new OlePropertySetBuilder.Prop(6, "SetDate", OlePropertySetBuilder.TypedFileTime(fileTime)),
            ]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        var map = result.Properties.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("-7", map["ShortValue"]);
        Assert.Equal("123456", map["LongValue"]);
        Assert.Equal("true", map["BoolTrue"]);
        Assert.Equal("false", map["BoolFalse"]);
        Assert.Equal(fileTime, DateTime.Parse(map["SetDate"]).ToUniversalTime());
    }

    [Fact]
    public void Decodes_vt_lpwstr_regardless_of_code_page()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "WideValue", OlePropertySetBuilder.TypedLpwstr("café"))]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        Assert.Equal("café", result.Properties.Single(p => p.Key == "WideValue").Value);
    }

    [Fact]
    public void Decodes_code_page_1200_as_utf16()
    {
        var bytes = OlePropertySetBuilder.Build(
            1200,
            [new OlePropertySetBuilder.Prop(2, "Naïve", OlePropertySetBuilder.TypedString("café", 1200))]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        var property = Assert.Single(result.Properties);
        Assert.Equal("Naïve", property.Key);
        Assert.Equal("café", property.Value);
    }

    [Fact]
    public void Decodes_code_page_65001_as_utf8()
    {
        var bytes = OlePropertySetBuilder.Build(
            65001,
            [new OlePropertySetBuilder.Prop(2, "café_Enabled", OlePropertySetBuilder.TypedString("true", 65001))]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        var property = Assert.Single(result.Properties);
        Assert.Equal("café_Enabled", property.Key);
        Assert.Equal("true", property.Value);
    }

    [Fact]
    public void Decodes_code_page_1252_high_range_explicitly()
    {
        // 0x80 => U+20AC (EURO SIGN) under Windows-1252.
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "Value", OlePropertySetBuilder.TypedStringFromRawBytes([0x80, 0x00]))]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        Assert.Equal("€", result.Properties.Single(p => p.Key == "Value").Value);
    }

    [Fact]
    public void Skips_a_value_that_requires_an_unsupported_code_page_without_failing_the_section()
    {
        // Code page 950 (Traditional Chinese Big5) is not explicitly supported. A high byte
        // under this code page cannot be decoded without guessing, so that single property is
        // left out - it does not invalidate the whole (structurally valid) section.
        var bytes = OlePropertySetBuilder.Build(
            950,
            [new OlePropertySetBuilder.Prop(2, "Value", OlePropertySetBuilder.TypedStringFromRawBytes([0x80, 0x00]))]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        Assert.Empty(result.Properties);
    }

    [Fact]
    public void A_non_ascii_value_under_an_unsupported_code_page_does_not_discard_an_ascii_msip_label_sibling()
    {
        // Regression test: a legacy file can contain a genuine ASCII MSIP label property
        // alongside an unrelated, genuinely non-ASCII custom property (e.g. Korean
        // records-management metadata) under the same section code page (949, unsupported).
        // Only the unreadable property must be dropped; the label must still come through.
        var bytes = OlePropertySetBuilder.Build(
            949,
            [
                new OlePropertySetBuilder.Prop(
                    2,
                    "MSIP_Label_6333fd3f-635c-44a8-824c-7f4faab3944a_Enabled",
                    OlePropertySetBuilder.TypedString("true", 949)),
                new OlePropertySetBuilder.Prop(
                    3,
                    "OtherMetadata",
                    OlePropertySetBuilder.TypedStringFromRawBytes([0x80, 0x00])),
            ]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        var property = Assert.Single(result.Properties);
        Assert.Equal("MSIP_Label_6333fd3f-635c-44a8-824c-7f4faab3944a_Enabled", property.Key);
        Assert.Equal("true", property.Value);
    }

    [Fact]
    public void A_dictionary_name_that_requires_an_unsupported_code_page_is_skipped_not_fatal()
    {
        // The same graceful-degradation rule applies to dictionary entry names, not just
        // values: a property whose *name* cannot be decoded under an unsupported code page
        // is left unnamed (and therefore invisible), but a sibling property with a decodable
        // ASCII name still comes through.
        var bytes = OlePropertySetBuilder.Build(
            949,
            [
                new OlePropertySetBuilder.Prop(2, "Known", OlePropertySetBuilder.TypedString("value", 949)),
                new OlePropertySetBuilder.Prop(
                    3,
                    ((char)0x80).ToString(), // Latin-1-encodes to a single 0x80 byte: non-ASCII.
                    OlePropertySetBuilder.TypedString("unreachable", 949)),
            ]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        var property = Assert.Single(result.Properties);
        Assert.Equal("Known", property.Key);
        Assert.Equal("value", property.Value);
    }

    [Fact]
    public void Skips_a_property_with_an_unrecognized_variant_type_without_failing_the_section()
    {
        const uint unsupportedVariantType = 0x0005; // VT_R8, not one of the supported types.
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [
                new OlePropertySetBuilder.Prop(2, "Known", OlePropertySetBuilder.TypedString("value", 1252)),
                new OlePropertySetBuilder.Prop(
                    3,
                    "Unknown",
                    OlePropertySetBuilder.Combine(
                        BitConverter.GetBytes(unsupportedVariantType),
                        BitConverter.GetBytes(0.0))),
            ]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        var property = Assert.Single(result.Properties);
        Assert.Equal("Known", property.Key);
        Assert.Equal("value", property.Value);
    }

    [Fact]
    public void Decodes_ascii_only_content_even_under_an_unrecognized_code_page()
    {
        // Real-world legacy Office files may declare a locale-specific code page (e.g. 949,
        // Korean) even though every MSIP_Label_* property name and value is pure ASCII. Since
        // ASCII (0x00-0x7F) is codepage-invariant, this is not guessing: it is decoded exactly
        // the same way under any of the explicitly supported code pages.
        var bytes = OlePropertySetBuilder.Build(
            949,
            [new OlePropertySetBuilder.Prop(2, "MSIP_Label_guid_Enabled", OlePropertySetBuilder.TypedString("true", 949))]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        Assert.Equal("true", result.Properties.Single(p => p.Key == "MSIP_Label_guid_Enabled").Value);
    }

    [Fact]
    public void Rejects_string_offset_beyond_section_bounds()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "Value", OlePropertySetBuilder.TypedString("true", 1252))]);
        OlePropertySetBuilder.CorruptPropertyDirectoryOffset(bytes, propertyIndex: 2, newOffset: bytes.Length + 1000);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Rejects_string_length_exceeding_section_bounds()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "Value", OlePropertySetBuilder.TypedString("true", 1252))]);
        OlePropertySetBuilder.CorruptStringLength(bytes, propertyIndex: 2, newLength: 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Rejects_missing_null_terminator()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "Value", OlePropertySetBuilder.TypedString("true", 1252))]);
        OlePropertySetBuilder.CorruptLastStringByte(bytes, propertyIndex: 2, newValue: (byte)'x');

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Rejects_nonzero_padding_after_fixed_size_value()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [new OlePropertySetBuilder.Prop(2, "BoolValue", OlePropertySetBuilder.TypedBool(true))]);
        OlePropertySetBuilder.CorruptFixedValuePadding(bytes, propertyIndex: 2);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Rejects_duplicate_property_identifiers()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [
                new OlePropertySetBuilder.Prop(2, "First", OlePropertySetBuilder.TypedString("a", 1252)),
                new OlePropertySetBuilder.Prop(2, "Second", OlePropertySetBuilder.TypedString("b", 1252)),
            ]);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Returns_properties_in_dictionary_declaration_order()
    {
        var bytes = OlePropertySetBuilder.Build(
            1252,
            [
                new OlePropertySetBuilder.Prop(5, "Zeta", OlePropertySetBuilder.TypedString("1", 1252)),
                new OlePropertySetBuilder.Prop(2, "Alpha", OlePropertySetBuilder.TypedString("2", 1252)),
                new OlePropertySetBuilder.Prop(3, "Beta", OlePropertySetBuilder.TypedString("3", 1252)),
            ]);

        var result = OlePropertySetReader.ReadUserDefinedProperties(bytes);

        Assert.Equal(OlePropertySetStatus.Success, result.Status);
        Assert.Equal(["Zeta", "Alpha", "Beta"], result.Properties.Select(p => p.Key));
    }

    [Fact]
    public void Rejects_property_set_count_over_the_limit()
    {
        var bytes = OlePropertySetBuilder.Build(1252, [], includeOtherSection: true);
        // MaxPropertySets is 2; the declared count is validated before the reader ever attempts
        // to read a (here, absent) third FMTID directory entry, so no third section is needed.
        OlePropertySetBuilder.CorruptPropertySetCount(bytes, 3);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Rejects_property_count_over_the_limit()
    {
        var bytes = OlePropertySetBuilder.Build(1252, []);
        // MaxProperties is 8192; the declared count is validated before the reader ever attempts
        // to read the (here, absent) property directory entries it describes.
        OlePropertySetBuilder.CorruptPropertyCount(bytes, 8193);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }

    [Fact]
    public void Rejects_dictionary_entry_count_over_the_limit()
    {
        var bytes = OlePropertySetBuilder.Build(1252, []);
        // MaxDictionaryEntries is 8192; the declared count is validated before the reader ever
        // attempts to read the (here, absent) dictionary entries it describes.
        OlePropertySetBuilder.CorruptDictionaryEntryCount(bytes, 8193);

        Assert.Throws<InvalidDataException>(() => OlePropertySetReader.ReadUserDefinedProperties(bytes));
    }
}

/// <summary>
/// Builds minimal, spec-shaped OLE property set stream bytes (MS-OLEPS) for testing
/// <see cref="OlePropertySetReader"/> without depending on a real compound file.
/// </summary>
internal static class OlePropertySetBuilder
{
    private static readonly Guid UserDefinedPropertiesFmtid = new("D5CDD505-2E9C-101B-9397-08002B2CF9AE");
    private static readonly Guid OtherFmtid = new("D5CDD502-2E9C-101B-9397-08002B2CF9AE");

    internal readonly record struct Prop(uint Id, string Name, byte[] Value);

    internal static byte[] Build(int codePage, IReadOnlyList<Prop> properties, bool includeOtherSection = false)
    {
        var targetSection = BuildSection(codePage, properties);
        var otherSection = includeOtherSection
            ? Combine(BitConverter.GetBytes(8u), BitConverter.GetBytes(0u))
            : [];

        var numSections = includeOtherSection ? 2 : 1;
        var directoryStart = 28;
        var directoryEnd = directoryStart + (numSections * 20);
        var otherOffset = directoryEnd;
        var targetOffset = directoryEnd + otherSection.Length;

        var header = new List<byte>();
        header.AddRange(BitConverter.GetBytes((ushort)0xFFFE));
        header.AddRange(BitConverter.GetBytes((ushort)0));
        header.AddRange(new byte[4]);
        header.AddRange(new byte[16]);
        header.AddRange(BitConverter.GetBytes((uint)numSections));

        if (includeOtherSection)
        {
            header.AddRange(OtherFmtid.ToByteArray());
            header.AddRange(BitConverter.GetBytes((uint)otherOffset));
        }

        header.AddRange(UserDefinedPropertiesFmtid.ToByteArray());
        header.AddRange(BitConverter.GetBytes((uint)targetOffset));

        if (includeOtherSection)
        {
            header.AddRange(otherSection);
        }

        header.AddRange(targetSection);
        return header.ToArray();
    }

    internal static byte[] BuildWithoutTargetSection()
    {
        var otherSection = Combine(BitConverter.GetBytes(8u), BitConverter.GetBytes(0u));

        var header = new List<byte>();
        header.AddRange(BitConverter.GetBytes((ushort)0xFFFE));
        header.AddRange(BitConverter.GetBytes((ushort)0));
        header.AddRange(new byte[4]);
        header.AddRange(new byte[16]);
        header.AddRange(BitConverter.GetBytes(1u));
        header.AddRange(OtherFmtid.ToByteArray());
        header.AddRange(BitConverter.GetBytes((uint)(28 + 20)));
        header.AddRange(otherSection);
        return header.ToArray();
    }

    internal static byte[] TypedString(string text, int codePage)
    {
        var (content, charLength) = EncodeName(text, codePage);
        return Combine(
            BitConverter.GetBytes(0x001Eu),
            BitConverter.GetBytes((uint)charLength),
            content);
    }

    internal static byte[] TypedStringFromRawBytes(byte[] rawBytesWithNullTerminator) =>
        Combine(
            BitConverter.GetBytes(0x001Eu),
            BitConverter.GetBytes((uint)rawBytesWithNullTerminator.Length),
            rawBytesWithNullTerminator);

    internal static byte[] TypedLpwstr(string text)
    {
        var content = Encoding.Unicode.GetBytes(text + "\0");
        var pad = (4 - (content.Length % 4)) % 4;
        return Combine(
            BitConverter.GetBytes(0x001Fu),
            BitConverter.GetBytes((uint)(text.Length + 1)),
            content,
            new byte[pad]);
    }

    internal static byte[] TypedI2(short value) =>
        Combine(BitConverter.GetBytes(0x0002u), BitConverter.GetBytes(value), new byte[2]);

    internal static byte[] TypedI4(int value) =>
        Combine(BitConverter.GetBytes(0x0003u), BitConverter.GetBytes(value));

    internal static byte[] TypedBool(bool value) =>
        Combine(BitConverter.GetBytes(0x000Bu), BitConverter.GetBytes((short)(value ? -1 : 0)), new byte[2]);

    internal static byte[] TypedFileTime(DateTime utc)
    {
        var fileTime = unchecked((ulong)utc.ToFileTimeUtc());
        return Combine(
            BitConverter.GetBytes(0x0040u),
            BitConverter.GetBytes((uint)(fileTime & 0xFFFFFFFF)),
            BitConverter.GetBytes((uint)(fileTime >> 32)));
    }

    internal static void CorruptPropertyDirectoryOffset(byte[] bytes, uint propertyIndex, int newOffset) =>
        WritePropertyOffset(bytes, propertyIndex, newOffset);

    internal static void CorruptStringLength(byte[] bytes, uint propertyIndex, int newLength)
    {
        var absOffset = FindPropertyAbsoluteOffset(bytes, propertyIndex);
        BitConverter.GetBytes((uint)newLength).CopyTo(bytes, absOffset + 4);
    }

    internal static void CorruptLastStringByte(byte[] bytes, uint propertyIndex, byte newValue)
    {
        var absOffset = FindPropertyAbsoluteOffset(bytes, propertyIndex);
        var length = BitConverter.ToUInt32(bytes, absOffset + 4);
        bytes[absOffset + 4 + 4 + checked((int)length) - 1] = newValue;
    }

    internal static void CorruptFixedValuePadding(byte[] bytes, uint propertyIndex)
    {
        var absOffset = FindPropertyAbsoluteOffset(bytes, propertyIndex);
        bytes[absOffset + 6] = 0x01;
    }

    internal static void CorruptPropertySetCount(byte[] bytes, uint newCount) =>
        BitConverter.GetBytes(newCount).CopyTo(bytes, 24);

    internal static void CorruptPropertyCount(byte[] bytes, uint newCount)
    {
        var secOffset = FindSectionOffset(bytes);
        BitConverter.GetBytes(newCount).CopyTo(bytes, secOffset + 4);
    }

    internal static void CorruptDictionaryEntryCount(byte[] bytes, uint newCount)
    {
        // The dictionary is always the property with id 0.
        var absOffset = FindPropertyAbsoluteOffset(bytes, propertyId: 0);
        BitConverter.GetBytes(newCount).CopyTo(bytes, absOffset);
    }

    private static int FindPropertyAbsoluteOffset(byte[] bytes, uint propertyId)
    {
        var secOffset = FindSectionOffset(bytes);
        var numProperties = BitConverter.ToUInt32(bytes, secOffset + 4);
        var directoryOffset = secOffset + 8;
        for (var i = 0; i < numProperties; i++)
        {
            var id = BitConverter.ToUInt32(bytes, directoryOffset);
            var offset = BitConverter.ToUInt32(bytes, directoryOffset + 4);
            if (id == propertyId)
            {
                return secOffset + checked((int)offset);
            }

            directoryOffset += 8;
        }

        throw new InvalidOperationException($"Property {propertyId} not found in test fixture.");
    }

    private static void WritePropertyOffset(byte[] bytes, uint propertyId, int newOffset)
    {
        var secOffset = FindSectionOffset(bytes);
        var numProperties = BitConverter.ToUInt32(bytes, secOffset + 4);
        var directoryOffset = secOffset + 8;
        for (var i = 0; i < numProperties; i++)
        {
            var id = BitConverter.ToUInt32(bytes, directoryOffset);
            if (id == propertyId)
            {
                BitConverter.GetBytes((uint)(newOffset - secOffset)).CopyTo(bytes, directoryOffset + 4);
                return;
            }

            directoryOffset += 8;
        }

        throw new InvalidOperationException($"Property {propertyId} not found in test fixture.");
    }

    private static int FindSectionOffset(byte[] bytes)
    {
        var numSections = BitConverter.ToUInt32(bytes, 24);
        var directoryOffset = 28;
        for (var i = 0; i < numSections; i++)
        {
            var fmtid = new Guid(bytes.AsSpan(directoryOffset, 16));
            var offset = BitConverter.ToUInt32(bytes, directoryOffset + 16);
            if (fmtid == UserDefinedPropertiesFmtid)
            {
                return checked((int)offset);
            }

            directoryOffset += 20;
        }

        throw new InvalidOperationException("Target FMTID section not found in test fixture.");
    }

    private static byte[] BuildSection(int codePage, IReadOnlyList<Prop> properties)
    {
        var directory = new List<(uint Id, byte[] Value)>
        {
            (1u, TypedI2((short)codePage)),
            (0u, BuildDictionary(codePage, properties)),
        };
        foreach (var prop in properties)
        {
            directory.Add((prop.Id, prop.Value));
        }

        var headerSize = 8 + (directory.Count * 8);
        var offsets = new int[directory.Count];
        var running = headerSize;
        for (var i = 0; i < directory.Count; i++)
        {
            offsets[i] = running;
            running += directory[i].Value.Length;
        }

        var section = new List<byte>();
        section.AddRange(BitConverter.GetBytes((uint)running));
        section.AddRange(BitConverter.GetBytes((uint)directory.Count));
        for (var i = 0; i < directory.Count; i++)
        {
            section.AddRange(BitConverter.GetBytes(directory[i].Id));
            section.AddRange(BitConverter.GetBytes((uint)offsets[i]));
        }

        foreach (var (_, value) in directory)
        {
            section.AddRange(value);
        }

        return section.ToArray();
    }

    private static byte[] BuildDictionary(int codePage, IReadOnlyList<Prop> properties)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes((uint)properties.Count));
        foreach (var prop in properties)
        {
            bytes.AddRange(BitConverter.GetBytes(prop.Id));
            var (content, charLength) = EncodeName(prop.Name, codePage);
            bytes.AddRange(BitConverter.GetBytes((uint)charLength));
            bytes.AddRange(content);
        }

        return bytes.ToArray();
    }

    private static (byte[] Content, int CharLength) EncodeName(string text, int codePage)
    {
        if (codePage == 1200)
        {
            var content = Encoding.Unicode.GetBytes(text + "\0");
            var pad = (4 - (content.Length % 4)) % 4;
            if (pad == 0)
            {
                return (content, text.Length + 1);
            }

            var padded = new byte[content.Length + pad];
            content.CopyTo(padded, 0);
            return (padded, text.Length + 1);
        }

        // For 65001 (UTF-8) the byte count and character count legitimately differ once
        // non-ASCII text is involved; for every other single-byte code page used in these
        // tests, Latin-1 gives an exact one-byte-per-character mapping, which is enough to
        // exercise the reader's byte-for-byte decoding without needing a full codepage table.
        var raw = codePage == 65001 ? Encoding.UTF8.GetBytes(text) : Encoding.Latin1.GetBytes(text);
        var withNull = new byte[raw.Length + 1];
        raw.CopyTo(withNull, 0);
        return (withNull, withNull.Length);
    }

    internal static byte[] Combine(params byte[][] parts)
    {
        var total = parts.Sum(part => part.Length);
        var result = new byte[total];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
