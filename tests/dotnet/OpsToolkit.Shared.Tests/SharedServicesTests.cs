using System.Text.Json;
using OpsToolkit.Shared.Models;
using OpsToolkit.Shared.Services;
using Xunit;

namespace OpsToolkit.Tests;

public sealed class SharedServicesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string Store => Path.Combine(_directory, "store.json");

    public SharedServicesTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public async Task PresetsUpsertAndKeepOneDefault()
    {
        var service = new PresetService(Store);
        var first = await service.SavePresetAsync(new("one", "", "{}", true));
        var second = await service.SavePresetAsync(new("two", "", "{}", true));
        first.Name = "updated";
        first.IsDefault = false;
        await service.SavePresetAsync(first);
        var profiles = await new PresetService(Store).GetPresetsAsync();
        Assert.Equal(2, profiles.Count);
        Assert.Equal(second.Id, Assert.Single(profiles, p => p.IsDefault).Id);
        Assert.Equal("updated", profiles.Single(p => p.Id == first.Id).Name);
        Assert.True(await service.DeletePresetAsync(first.Id));
        Assert.False(await service.DeletePresetAsync(first.Id));
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{\"Unexpected\":\"preserve-this-data\"}]")]
    public async Task MalformedStoresAreNeverOverwritten(string json)
    {
        await File.WriteAllTextAsync(Store, json);
        var presets = new PresetService(Store);
        await Assert.ThrowsAsync<JsonException>(() => presets.GetPresetsAsync());
        await Assert.ThrowsAsync<JsonException>(() => presets.SavePresetAsync(new("one", "", "{}")));
        await Assert.ThrowsAsync<JsonException>(() => presets.DeletePresetAsync("id"));
        var history = new RecentHistoryService(Store);
        await Assert.ThrowsAsync<JsonException>(() => history.AddItemAsync("path"));
        await Assert.ThrowsAsync<JsonException>(() => history.ClearHistoryAsync());
        Assert.Equal(json, await File.ReadAllTextAsync(Store));
    }

    [Fact]
    public async Task HistoryIsBoundedDeduplicatedAndReloaded()
    {
        var service = new RecentHistoryService(Store);
        for (int i = 0; i < 12; i++)
        {
            await service.AddItemAsync($"item{i}");
        }
        await service.AddItemAsync("ITEM5", "Check", false, "note");
        var items = await new RecentHistoryService(Store).GetHistoryAsync();
        Assert.Equal(10, items.Count);
        Assert.Equal("Check", items[0].ActionType);
        Assert.False(items[0].IsSuccess);
        Assert.Single(items, i => i.Path.Equals("item5", StringComparison.OrdinalIgnoreCase));
        await service.ClearHistoryAsync();
        Assert.Empty(await service.GetHistoryAsync());
    }

    [Fact]
    public async Task CanceledWritesPreserveStoreAndLeaveNoTemporaryFiles()
    {
        var service = new PresetService(Store);
        await service.SavePresetAsync(new("one", "", "{}"));
        var before = await File.ReadAllTextAsync(Store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SavePresetAsync(new("two", "", "{}"), cancellation.Token));
        Assert.Equal(before, await File.ReadAllTextAsync(Store));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task StorageRejectsInvalidSettingsWithoutChangingExistingData()
    {
        var service = new PresetService(Store);
        await Assert.ThrowsAnyAsync<JsonException>(() => service.SavePresetAsync(new("invalid", "", "{")));
        Assert.False(File.Exists(Store));
        Assert.Throws<ArgumentException>(() => new PresetService(" "));
        Assert.Throws<ArgumentException>(() => new RecentHistoryService(" "));
    }

    [Fact]
    public async Task TailWaitsForCompleteUtf8LineAndDetectsReplacement()
    {
        await File.WriteAllTextAsync(Store, "old\n한국");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lines = new LogTailService().ReadLinesAsync(Store, 1, TimeSpan.FromMilliseconds(10), cancellation.Token).GetAsyncEnumerator();
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("old", lines.Current);
        await File.AppendAllTextAsync(Store, "어\n");
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("한국어", lines.Current);
        await File.WriteAllTextAsync(Store, "replacement\n");
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("replacement", lines.Current);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await lines.MoveNextAsync());
    }

    [Fact]
    public async Task TailTrimsBomEvenWhenFirstLineCrossesReadBuffer()
    {
        var longLine = new string('x', 5000);
        await File.WriteAllTextAsync(Store, "\uFEFF" + longLine + "\nsecond\n");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var lines = new LogTailService().ReadLinesAsync(Store, 2, TimeSpan.FromMilliseconds(10), cancellation.Token).GetAsyncEnumerator();
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal(longLine, lines.Current);
    }

    [Fact]
    public async Task TailStartsAtLimitedHistoryAndResetsAfterTruncation()
    {
        await File.WriteAllTextAsync(Store, "one\ntwo\nthree\npartial");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var lines = new LogTailService().ReadLinesAsync(Store, 1, TimeSpan.FromMilliseconds(10), cancellation.Token).GetAsyncEnumerator();
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("three", lines.Current);
        await File.WriteAllTextAsync(Store, "new\n");
        Assert.True(await lines.MoveNextAsync());
        Assert.Equal("new", lines.Current);
    }
    [Fact]
    public void ReportsEscapeCsvAndSerializePlainDto()
    {
        var service = new ReportExportService();
        ReportItem[] items = [new() { FileName = "=formula", FilePath = "a,\"b\"\nc", Status = "Completed", ProgressPercent = 12.5 }];
        var csv = service.ExportToCsv(items);
        Assert.Contains("'=formula", csv);
        Assert.Contains("\"a,\"\"b\"\"\nc\"", csv);
        Assert.Contains("12.5", csv);
        var restored = JsonSerializer.Deserialize<ReportItem[]>(service.ExportToJson(items));
        Assert.Equal(items[0].FilePath, Assert.Single(restored!).FilePath);
    }

    [Fact]
    public void DiagnosticsTextReportIncludesProcessorAndRuntimeMemoryBudget()
    {
        var service = new SystemDiagnosticsService();
        var info = service.GetDiagnostics();
        var report = service.GenerateTextReport();
        Assert.Contains(info.ProcessorName, report);
        Assert.Contains(info.OsVersion, report);
        Assert.Contains(info.DotNetRuntimeVersion, report);
        Assert.Contains(info.MachineName, report);
        Assert.Contains("Runtime available memory budget (bytes):", report);
        Assert.Contains("Current process working set (bytes):", report);
        Assert.DoesNotContain("physical RAM", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticsAreLocalAndDescribeAvailableMemoryAccurately()
    {
        var info = new SystemDiagnosticsService().GetDiagnostics();
        Assert.True(info.ProcessorCount > 0);
        Assert.NotEmpty(info.ProcessorName);
        Assert.NotEmpty(info.OsVersion);
        Assert.NotEmpty(info.DotNetRuntimeVersion);
        Assert.True(info.CurrentProcessMemoryBytes > 0);
        Assert.True(info.AvailableMemoryBytes > 0);
    }
}
