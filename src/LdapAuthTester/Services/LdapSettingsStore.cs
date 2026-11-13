using System.Text.Json;
using LdapAuthTester.Models;
using OpsToolkit.Shared.Models;
using OpsToolkit.Shared.Services;

namespace LdapAuthTester.Services;

public sealed class LdapSettingsStore : IDisposable
{
    private readonly PresetService _presets;
    private readonly RecentHistoryService _history;

    public LdapSettingsStore(string directory)
    {
        _presets = new PresetService(Path.Combine(directory, "presets.json"));
        _history = new RecentHistoryService(Path.Combine(directory, "history.json"));
    }

    public async Task<IReadOnlyList<PresetProfile>> GetPresetsAsync(CancellationToken cancellationToken = default)
    {
        var presets = await _presets.GetPresetsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var preset in presets)
        {
            LdapServerSettings.Deserialize(preset.SettingsJson);
        }
        return presets;
    }

    public async Task SavePresetAsync(LdapServerSettings settings, string? id = null, CancellationToken cancellationToken = default)
    {
        settings.Validate();
        await GetPresetsAsync(cancellationToken).ConfigureAwait(false);
        var preset = new PresetProfile { Name = settings.Domain, SettingsJson = JsonSerializer.Serialize(settings) };
        if (id is not null)
        {
            preset.Id = id;
        }
        await _presets.SavePresetAsync(preset, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> DeletePresetAsync(string id, CancellationToken cancellationToken = default)
        => _presets.DeletePresetAsync(id, cancellationToken);

    public async Task<IReadOnlyList<string>> GetRecentServersAsync(CancellationToken cancellationToken = default)
    {
        var history = await _history.GetHistoryAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in history)
        {
            LdapServerSettings.ValidateServer(item.Path);
        }
        return history.Select(item => item.Path).ToArray();
    }

    public Task AddRecentServerAsync(string server, CancellationToken cancellationToken = default)
    {
        LdapServerSettings.ValidateServer(server);
        return _history.AddItemAsync(server, actionType: "Server", cancellationToken: cancellationToken);
    }

    public Task ClearRecentServersAsync(CancellationToken cancellationToken = default)
        => _history.ClearHistoryAsync(cancellationToken);

    public void Dispose()
    {
        _presets.Dispose();
        _history.Dispose();
    }
}
