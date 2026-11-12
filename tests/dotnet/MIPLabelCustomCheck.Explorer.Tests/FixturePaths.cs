using System.IO;

namespace MIPLabelCustomCheck.Explorer.Tests;

internal static class FixturePaths
{
    internal static string Get(string extension) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", $"MIP Label{extension}");
}
