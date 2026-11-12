using MIPLabelCustomCheck.Internal.Pdf;
using MIPLabelCustomCheck.Tests.TestData;

namespace MIPLabelCustomCheck.Tests;

public sealed class PdfCrossReferenceReaderTests
{
    [Fact]
    public void Opens_a_classic_xref_and_resolves_the_root_dictionary()
    {
        using var stream = PdfFixture.WithClassicXref();

        var document = PdfObjectResolver.Open(stream);
        var trailer = document.LatestTrailer;

        Assert.True(trailer.TryGetReference("Root", out var root));
        Assert.IsType<PdfDictionary>(document.Resolve(root));
    }

    [Fact]
    public void Follows_prev_to_merge_an_older_revisions_objects()
    {
        using var stream = PdfFixture.WithClassicXrefAndPrev();
        var document = PdfObjectResolver.Open(stream);
        var trailer = document.LatestTrailer;
        Assert.True(trailer.TryGetReference("Root", out var root));

        var catalog = Assert.IsType<PdfDictionary>(document.Resolve(root));
        var marker = Assert.IsType<PdfString>(catalog.Items["Marker"]);
        Assert.Equal("v2"u8.ToArray(), marker.Bytes);

        var onlyInOldRevision = Assert.IsType<PdfDictionary>(
            document.Resolve(new PdfReference(2, 0)));
        Assert.Equal(new PdfBoolean(true), onlyInOldRevision.Items["OnlyInOldRevision"]);
    }

    [Fact]
    public void Resolves_objects_from_both_subsections_of_a_multi_subsection_classic_xref()
    {
        using var stream = PdfFixture.WithClassicXrefTwoSubsections();
        var document = PdfObjectResolver.Open(stream);
        var trailer = document.LatestTrailer;

        Assert.True(trailer.TryGetReference("Root", out var root));
        Assert.IsType<PdfDictionary>(document.Resolve(root));

        var info = Assert.IsType<PdfDictionary>(document.Resolve(new PdfReference(4, 0)));
        var marker = Assert.IsType<PdfString>(info.Items["Marker"]);
        Assert.Equal("second-subsection"u8.ToArray(), marker.Bytes);
    }

    [Fact]
    public void Resolves_the_root_dictionary_from_a_cross_reference_stream_with_w_and_index()
    {
        using var stream = PdfFixture.WithXrefStream();
        var document = PdfObjectResolver.Open(stream);
        var trailer = document.LatestTrailer;

        Assert.True(trailer.TryGetReference("Root", out var root));
        Assert.IsType<PdfDictionary>(document.Resolve(root));
    }

    [Fact]
    public void Resolves_objects_from_both_runs_of_a_two_run_index_cross_reference_stream()
    {
        using var stream = PdfFixture.WithXrefStreamTwoIndexRuns();
        var document = PdfObjectResolver.Open(stream);
        var trailer = document.LatestTrailer;

        Assert.True(trailer.TryGetReference("Root", out var root));
        Assert.IsType<PdfDictionary>(document.Resolve(root));

        var info = Assert.IsType<PdfDictionary>(document.Resolve(new PdfReference(6, 0)));
        var marker = Assert.IsType<PdfString>(info.Items["Marker"]);
        Assert.Equal("second-run"u8.ToArray(), marker.Bytes);
    }

    [Fact]
    public void Resolves_a_type_two_entry_from_an_object_stream()
    {
        using var stream = PdfFixture.WithObjectStream();
        var document = PdfObjectResolver.Open(stream);

        var resolved = Assert.IsType<PdfDictionary>(document.Resolve(new PdfReference(3, 0)));

        var kind = Assert.IsType<PdfString>(resolved.Items["Kind"]);
        Assert.Equal("Compressed"u8.ToArray(), kind.Bytes);
    }

    [Fact]
    public void Caches_resolved_objects_within_a_single_document()
    {
        using var stream = PdfFixture.WithClassicXref();
        var document = PdfObjectResolver.Open(stream);
        Assert.True(document.LatestTrailer.TryGetReference("Root", out var root));

        var first = document.Resolve(root);
        var second = document.Resolve(root);

        Assert.Same(first, second);
    }

    [Fact]
    public void Throws_for_an_unsupported_cross_reference_stream_filter()
    {
        using var stream = PdfFixture.WithUnsupportedXrefStreamFilter();

        Assert.Throws<PdfUnsupportedFeatureException>(() => PdfObjectResolver.Open(stream));
    }

    [Fact]
    public void Throws_for_an_invalid_startxref_offset()
    {
        using var stream = PdfFixture.WithInvalidStartXrefOffset();

        Assert.Throws<InvalidDataException>(() => PdfObjectResolver.Open(stream));
    }

    [Fact]
    public void Throws_for_a_cyclic_prev_chain()
    {
        using var stream = PdfFixture.WithCyclicPrev();

        Assert.Throws<InvalidDataException>(() => PdfObjectResolver.Open(stream));
    }

    [Fact]
    public void Throws_when_resolving_an_out_of_range_object_offset()
    {
        using var stream = PdfFixture.WithOutOfRangeObjectOffset();
        var document = PdfObjectResolver.Open(stream);

        Assert.Throws<InvalidDataException>(() => document.Resolve(new PdfReference(2, 0)));
    }

    [Fact]
    public void Throws_when_resolving_an_oversized_stream_length()
    {
        using var stream = PdfFixture.WithOversizedStreamLength();
        var document = PdfObjectResolver.Open(stream);

        Assert.Throws<InvalidDataException>(() => document.Resolve(new PdfReference(2, 0)));
    }
}
