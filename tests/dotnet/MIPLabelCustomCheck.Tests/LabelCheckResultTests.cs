namespace MIPLabelCustomCheck.Tests;

public sealed class LabelCheckResultTests
{
    private static readonly Guid FirstLabelId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondLabelId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DifferentLabelId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Assert_equal_uses_label_sequence_values()
    {
        var first = Create(FirstLabelId, SecondLabelId);
        var second = Create(FirstLabelId, SecondLabelId);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equality_operator_uses_label_sequence_values()
    {
        var first = Create(FirstLabelId, SecondLabelId);
        var second = Create(FirstLabelId, SecondLabelId);

        Assert.True(first == second);
    }

    [Fact]
    public void Equal_results_have_equal_hash_codes()
    {
        var first = Create(FirstLabelId, SecondLabelId);
        var second = Create(FirstLabelId, SecondLabelId);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Different_label_order_is_not_equal()
    {
        var first = Create(FirstLabelId, SecondLabelId);
        var second = Create(SecondLabelId, FirstLabelId);

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void Different_label_id_is_not_equal()
    {
        var first = Create(FirstLabelId, SecondLabelId);
        var second = Create(FirstLabelId, DifferentLabelId);

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    private static LabelCheckResult Create(params Guid[] labelIds) =>
        new(LabelStatus.Labeled, labelIds, ProtectionType.Native);

    [Fact]
    public void Sources_run_parallel_to_label_ids()
    {
        var result = new LabelCheckResult(
            LabelStatus.Labeled,
            [FirstLabelId, SecondLabelId],
            ProtectionType.None,
            null,
            [LabelSource.PdfInfoDictionary, LabelSource.Xmp]);

        Assert.Equal(
            [LabelSource.PdfInfoDictionary, LabelSource.Xmp],
            result.Sources);
    }

    [Fact]
    public void Sources_default_to_empty_when_not_provided()
    {
        Assert.Empty(Create(FirstLabelId).Sources);
    }

    [Fact]
    public void ProtectionDetail_defaults_to_empty_when_not_provided()
    {
        Assert.Equal(string.Empty, Create(FirstLabelId).ProtectionDetail);
    }

    [Fact]
    public void Different_sources_are_not_equal()
    {
        var first = new LabelCheckResult(
            LabelStatus.Labeled, [FirstLabelId], ProtectionType.None, null, [LabelSource.Xmp]);
        var second = new LabelCheckResult(
            LabelStatus.Labeled, [FirstLabelId], ProtectionType.None, null, [LabelSource.PdfInfoDictionary]);

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Different_protection_detail_is_not_equal()
    {
        var first = new LabelCheckResult(
            LabelStatus.ProtectedLabelUnknown,
            [],
            ProtectionType.Native,
            protectionDetail: "DataSpaces");
        var second = new LabelCheckResult(
            LabelStatus.ProtectedLabelUnknown,
            [],
            ProtectionType.Native,
            protectionDetail: "EncryptedPackage");

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.GetHashCode(), second.GetHashCode());
    }
}
