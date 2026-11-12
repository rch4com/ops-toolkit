using System.Text;
using MIPLabelCustomCheck.Internal.Pdf;

namespace MIPLabelCustomCheck.Tests;

public sealed class PdfLexerTests
{
    [Fact]
    public void Reads_a_name()
    {
        var value = ReadValue("/Type");

        Assert.Equal(new PdfName("Type"), value);
    }

    [Fact]
    public void Reads_a_name_with_hash_escapes()
    {
        var value = ReadValue("/Name#20With#20Spaces");

        Assert.Equal(new PdfName("Name With Spaces"), value);
    }

    [Fact]
    public void Reads_an_empty_name()
    {
        var value = ReadValue("/ ");

        Assert.Equal(new PdfName(string.Empty), value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Reads_a_boolean(string text, bool expected)
    {
        var value = ReadValue(text);

        Assert.Equal(new PdfBoolean(expected), value);
    }

    [Fact]
    public void Reads_the_null_keyword()
    {
        var value = ReadValue("null");

        Assert.Equal(new PdfNull(), value);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("+7", 7)]
    [InlineData("-45", -45)]
    [InlineData("0", 0)]
    public void Reads_an_integer(string text, long expected)
    {
        var value = ReadValue(text);

        Assert.Equal(new PdfNumber(expected), value);
    }

    [Theory]
    [InlineData("612.0")]
    [InlineData(".5")]
    [InlineData("-.5")]
    [InlineData("-3.14")]
    public void Reads_a_top_level_real_number_as_unsupported_not_corrupt(string text)
    {
        Assert.Throws<PdfUnsupportedFeatureException>(() => ReadValue(text));
    }

    [Fact]
    public void Reads_a_real_number_inside_an_array_as_unsupported_not_corrupt()
    {
        // e.g. /MediaBox [0 0 612.0 792.0] — a real number is valid, common PDF syntax, not
        // corruption; the closed PdfValue model just doesn't implement a real-number type.
        Assert.Throws<PdfUnsupportedFeatureException>(() => ReadValue("[0 0 612.0 792.0]"));
    }

    [Fact]
    public void Reads_a_leading_dot_real_number_inside_an_array_as_unsupported_not_corrupt()
    {
        Assert.Throws<PdfUnsupportedFeatureException>(() => ReadValue("[0 .5]"));
    }

    [Fact]
    public void Reads_a_real_number_inside_a_dictionary_value_as_unsupported_not_corrupt()
    {
        Assert.Throws<PdfUnsupportedFeatureException>(() => ReadValue("<< /Value 612.0 >>"));
    }

    [Fact]
    public void Reads_a_literal_string_with_nested_parentheses()
    {
        var value = ReadValue("(Outer (Inner) Text)");

        Assert.Equal(Encoding.ASCII.GetBytes("Outer (Inner) Text"), Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_a_literal_string_with_standard_escapes()
    {
        var value = ReadValue(@"(Line1\nLine2\tTab\)\(\\)");

        Assert.Equal(
            Encoding.ASCII.GetBytes("Line1\nLine2\tTab)(\\"),
            Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_a_literal_string_with_octal_escapes()
    {
        var value = ReadValue(@"(\051\050)");

        Assert.Equal(Encoding.ASCII.GetBytes(")("), Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_a_literal_string_with_a_line_continuation_escape()
    {
        var value = ReadValue("(Line1\\\nLine2)");

        Assert.Equal(Encoding.ASCII.GetBytes("Line1Line2"), Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_a_literal_string_normalizing_raw_carriage_returns_to_line_feeds()
    {
        var value = ReadValue("(A\rB)");

        Assert.Equal(Encoding.ASCII.GetBytes("A\nB"), Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_a_hex_string()
    {
        var value = ReadValue("<48656C6C6F>");

        Assert.Equal(Encoding.ASCII.GetBytes("Hello"), Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_a_hex_string_with_whitespace_and_pads_an_odd_final_digit()
    {
        var value = ReadValue("<48 65 6C 6C 6F 2>");

        Assert.Equal(
            [.. Encoding.ASCII.GetBytes("Hello"), 0x20],
            Assert.IsType<PdfString>(value).Bytes);
    }

    [Fact]
    public void Reads_an_array()
    {
        var value = ReadValue("[1 2 3]");

        Assert.Equal(
            new PdfValue[] { new PdfNumber(1), new PdfNumber(2), new PdfNumber(3) },
            Assert.IsType<PdfArray>(value).Items);
    }

    [Fact]
    public void Reads_a_dictionary()
    {
        var value = ReadValue("<< /Foo 1 /Bar (baz) >>");

        var dictionary = Assert.IsType<PdfDictionary>(value);
        Assert.Equal(new PdfNumber(1), dictionary.Items["Foo"]);
        Assert.Equal(
            Encoding.ASCII.GetBytes("baz"),
            Assert.IsType<PdfString>(dictionary.Items["Bar"]).Bytes);
    }

    [Fact]
    public void Reads_an_indirect_reference()
    {
        var value = ReadValue("12 0 R");

        Assert.Equal(new PdfReference(12, 0), value);
    }

    [Fact]
    public void Does_not_misread_two_adjacent_numbers_in_an_array_as_a_reference()
    {
        var value = ReadValue("[12 0]");

        Assert.Equal(
            new PdfValue[] { new PdfNumber(12), new PdfNumber(0) },
            Assert.IsType<PdfArray>(value).Items);
    }

    [Fact]
    public void Skips_comments_before_a_value()
    {
        var value = ReadValue("% a comment\n123");

        Assert.Equal(new PdfNumber(123), value);
    }

    [Fact]
    public void Skips_comments_inside_an_array()
    {
        var value = ReadValue("[1 % comment\n 2]");

        Assert.Equal(
            new PdfValue[] { new PdfNumber(1), new PdfNumber(2) },
            Assert.IsType<PdfArray>(value).Items);
    }

    [Fact]
    public void Throws_when_nesting_exceeds_the_configured_depth()
    {
        var text = new string('[', PdfLexer.MaxNestingDepth + 2) +
            new string(']', PdfLexer.MaxNestingDepth + 2);

        Assert.Throws<InvalidDataException>(() => ReadValue(text));
    }

    [Fact]
    public void Throws_when_an_array_exceeds_the_item_limit()
    {
        var text = "[" + string.Concat(Enumerable.Repeat("0 ", PdfLexer.MaxCollectionItems + 1)) + "]";

        Assert.Throws<InvalidDataException>(() => ReadValue(text));
    }

    [Fact]
    public void Throws_when_a_name_exceeds_the_byte_limit()
    {
        var text = "/" + new string('A', PdfLexer.MaxNameBytes + 1);

        Assert.Throws<InvalidDataException>(() => ReadValue(text));
    }

    [Fact]
    public void Throws_when_a_string_exceeds_the_byte_limit()
    {
        var text = "(" + new string('A', PdfLexer.MaxStringBytes + 1) + ")";

        Assert.Throws<InvalidDataException>(() => ReadValue(text));
    }

    [Fact]
    public void Reads_an_indirect_object_without_a_stream()
    {
        var bytes = Encoding.ASCII.GetBytes("7 0 obj\n<< /Foo 1 >>\nendobj\n");
        var lexer = new PdfLexer(bytes);

        var result = lexer.ReadIndirectObject();

        Assert.Equal(7, result.ObjectNumber);
        Assert.Equal(0, result.Generation);
        var dictionary = Assert.IsType<PdfDictionary>(result.Value);
        Assert.Equal(new PdfNumber(1), dictionary.Items["Foo"]);
    }

    [Fact]
    public void Reads_an_indirect_object_with_a_stream()
    {
        var bytes = Encoding.ASCII.GetBytes("9 0 obj\n<< /Length 5 >>\nstream\nHello\nendstream\nendobj\n");
        var lexer = new PdfLexer(bytes);

        var result = lexer.ReadIndirectObject();

        var stream = Assert.IsType<PdfStream>(result.Value);
        Assert.Equal(Encoding.ASCII.GetBytes("Hello"), stream.Data);
    }

    [Fact]
    public void Throws_when_an_indirect_object_declares_an_oversized_stream_length()
    {
        var bytes = Encoding.ASCII.GetBytes(
            $"9 0 obj\n<< /Length {PdfLexer.MaxStreamBytes + 1} >>\nstream\nHello\nendstream\nendobj\n");
        var lexer = new PdfLexer(bytes);

        Assert.Throws<InvalidDataException>(() => lexer.ReadIndirectObject());
    }

    [Fact]
    public void Throws_a_distinct_exception_for_an_indirect_stream_length()
    {
        var bytes = Encoding.ASCII.GetBytes("9 0 obj\n<< /Length 5 0 R >>\nstream\nHello\nendstream\nendobj\n");
        var lexer = new PdfLexer(bytes);

        Assert.Throws<PdfUnsupportedFeatureException>(() => lexer.ReadIndirectObject());
    }

    [Fact]
    public void Throws_when_endobj_is_missing()
    {
        var bytes = Encoding.ASCII.GetBytes("3 0 obj\n<< /Foo 1 >>\n");
        var lexer = new PdfLexer(bytes);

        Assert.Throws<InvalidDataException>(() => lexer.ReadIndirectObject());
    }

    private static PdfValue ReadValue(string text) =>
        new PdfLexer(Encoding.ASCII.GetBytes(text)).ReadValue();
}
