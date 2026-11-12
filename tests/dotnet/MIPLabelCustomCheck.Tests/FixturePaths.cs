namespace MIPLabelCustomCheck.Tests;

internal static class FixturePaths
{
    internal static string Get(string extension) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", $"MIP Label{extension}");
}
