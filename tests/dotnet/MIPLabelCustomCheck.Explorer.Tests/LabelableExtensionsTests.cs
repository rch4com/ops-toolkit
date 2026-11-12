using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.Tests;

public sealed class LabelableExtensionsTests
{
    /// <summary>
    /// The generic-protection container is the one extension the default set must never lose:
    /// every file type Purview protects that has no native support becomes one of these.
    /// </summary>
    [Fact]
    public void Default_includes_the_generic_pfile_container() =>
        Assert.Contains(".pfile", LabelableExtensions.Default);

    [Theory]
    // 이 라이브러리가 실제로 레이블을 읽어내는 형식들.
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    [InlineData(".pdf")]
    [InlineData(".png")]
    // 매크로 사용 및 서식 파일 변형.
    [InlineData(".docm")]
    [InlineData(".dotx")]
    [InlineData(".xlsm")]
    [InlineData(".xltx")]
    [InlineData(".pptm")]
    [InlineData(".potx")]
    // Purview가 레이블하지만 이 라이브러리는 읽지 못하는 형식들 — 기본 집합에는 들어간다.
    [InlineData(".jpg")]
    [InlineData(".tiff")]
    [InlineData(".psd")]
    [InlineData(".vsdx")]
    [InlineData(".mpp")]
    [InlineData(".txt")]
    public void Default_includes_purview_labelable_extension(string extension) =>
        Assert.Contains(extension, LabelableExtensions.Default);

    [Theory]
    // Purview가 분류·레이블에서 제외하는 형식들. 이것들이 들어오면 스캔이 노이즈로 가득 찬다.
    [InlineData(".exe")]
    [InlineData(".dll")]
    [InlineData(".sys")]
    [InlineData(".msi")]
    [InlineData(".bat")]
    [InlineData(".tmp")]
    public void Default_excludes_extension_purview_never_labels(string extension) =>
        Assert.DoesNotContain(extension, LabelableExtensions.Default);

    [Fact]
    public void Default_is_case_insensitive() =>
        Assert.Contains(".DOCX", LabelableExtensions.Default);

    [Fact]
    public void Includes_matches_a_listed_extension_regardless_of_case() =>
        Assert.True(LabelableExtensions.Includes(LabelableExtensions.Default, @"C:\x\Report.DOCX"));

    [Fact]
    public void Includes_rejects_an_unlisted_extension() =>
        Assert.False(LabelableExtensions.Includes(LabelableExtensions.Default, @"C:\x\kernel32.dll"));

    [Fact]
    public void Includes_rejects_a_file_without_an_extension() =>
        Assert.False(LabelableExtensions.Includes(LabelableExtensions.Default, @"C:\x\LICENSE"));

    [Theory]
    [InlineData(".docx,.pdf")]
    [InlineData("docx, pdf")]
    [InlineData(" .docx ; pdf ")]
    [InlineData(".docx\n.pdf")]
    public void Parse_reads_the_separators_and_dots_a_user_might_type(string text)
    {
        var parsed = LabelableExtensions.Parse(text);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(".docx", parsed);
        Assert.Contains(".pdf", parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData(".")]
    [InlineData(", ;")]
    public void Parse_yields_nothing_for_blank_or_meaningless_input(string? text) =>
        Assert.Empty(LabelableExtensions.Parse(text));

    [Fact]
    public void Format_and_parse_round_trip_the_default_set()
    {
        var roundTripped = LabelableExtensions.Parse(LabelableExtensions.Format(LabelableExtensions.Default));

        Assert.Equal(
            LabelableExtensions.Default.Order(StringComparer.OrdinalIgnoreCase),
            roundTripped.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Format_sorts_so_the_box_reads_predictably() =>
        Assert.Equal(".a, .b, .c", LabelableExtensions.Format([".c", ".a", ".b"]));
}
