using System.Text.Json;
using LdapAuthTester.Models;
using LdapAuthTester.Services;
using Xunit;

namespace LdapAuthTester.Tests;

public sealed class LdapSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SavedSettingsExcludeCredentialsAndRunOnlyOptionsAndReload()
    {
        using var store = new LdapSettingsStore(_directory);
        var request = new AdAuthTestRequest
        {
            Domain = "localhost",
            UserName = "private-user",
            Password = "private-password",
            IncludeUserInfo = true,
            SkipCertificateValidation = true,
            TimeoutSeconds = 12
        };
        await store.SavePresetAsync(LdapServerSettings.FromRequest(request));
        string json = await File.ReadAllTextAsync(Path.Combine(_directory, "presets.json"));
        Assert.DoesNotContain("private-", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SkipCertificateValidation", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IncludeUserInfo", json, StringComparison.Ordinal);
        using var reloaded = new LdapSettingsStore(_directory);
        var presets = await reloaded.GetPresetsAsync();
        Assert.Single(presets);
        Assert.Equal(12, LdapServerSettings.Deserialize(presets[0].SettingsJson).TimeoutSeconds);
        await reloaded.DeletePresetAsync(presets[0].Id);
        Assert.Empty(await reloaded.GetPresetsAsync());
    }

    [Fact]
    public async Task RecentServersAreDeduplicatedLimitedAndExplicitlyCleared()
    {
        using var store = new LdapSettingsStore(_directory);
        for (int i = 0; i < 12; i++)
        {
            await store.AddRecentServerAsync($"server{i}.example.test");
        }
        await store.AddRecentServerAsync("server11.example.test");
        Assert.Equal(10, (await store.GetRecentServersAsync()).Count);
        await store.ClearRecentServersAsync();
        Assert.Empty(await store.GetRecentServersAsync());
    }

    [Fact]
    public async Task CorruptStoreIsReportedWithoutOverwriting()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "presets.json");
        await File.WriteAllTextAsync(path, "broken");
        using var store = new LdapSettingsStore(_directory);
        await Assert.ThrowsAsync<JsonException>(() => store.GetPresetsAsync());
        await Assert.ThrowsAsync<JsonException>(() => store.SavePresetAsync(new() { Domain = "localhost" }));
        Assert.Equal("broken", await File.ReadAllTextAsync(path));
    }

    [Theory]
    [InlineData("{\"Domain\":\"localhost\",\"Password\":\"secret\"}")]
    [InlineData("{\"Domain\":\"localhost\",\"SkipCertificateValidation\":true}")]
    [InlineData("{\"Domain\":\"localhost\",\"TimeoutSeconds\":0}")]
    public void SettingsRejectUnknownOrInvalidFields(string json)
    {
        Assert.ThrowsAny<Exception>(() => LdapServerSettings.Deserialize(json));
    }

    [Fact]
    public async Task ClosingFormWhileSettingsActionAwaitsDrainsBeforeCleanup()
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var form = new MainForm();
                SynchronizationContext.SetSynchronizationContext(null);
                var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var action = typeof(MainForm).GetMethod("RunSettingsActionAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                Task pending = (Task)action.Invoke(form, [new Func<Task>(() => hold.Task)])!;
                form.Dispose();
                var disposed = typeof(MainForm).GetField("_resourcesDisposed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(disposed);
                Assert.False((bool)disposed.GetValue(form)!);
                hold.SetResult();
                pending.GetAwaiter().GetResult();
                Assert.True((bool)disposed.GetValue(form)!);
                finished.SetResult();
            }
            catch (Exception ex) { finished.SetException(ex); }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
