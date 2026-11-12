using MIPLabelCustomCheck.Explorer.Scanning;
using MIPLabelCustomCheck.Explorer.ViewModels;

namespace MIPLabelCustomCheck.Explorer.Tests;

public sealed class FileEntryViewModelTests
{
    [Fact]
    public void SourcesText_shows_xmp_for_labeled_png_fixture()
    {
        var path = FixturePaths.Get(".png");
        var result = new MipLabelChecker().Check(path);
        var entry = new FileEntryViewModel(path, 0);
        entry.Apply(new TimedCheckResult(result, null, TimeSpan.Zero));

        Assert.Equal("XMP", entry.SourcesText);
    }

    [Fact]
    public void SourcesText_shows_dash_when_no_result()
    {
        var entry = new FileEntryViewModel(FixturePaths.Get(".png"), 0);

        Assert.Equal("-", entry.SourcesText);
    }
}
