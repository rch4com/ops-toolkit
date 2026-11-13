using System.Text.Json;
using OpsToolkit.Shared.Models;

namespace OpsToolkit.Shared.Services;

public sealed class PresetService(string filePath) : IDisposable
{
    private readonly JsonFileStore<PresetProfile> _store = new(filePath);

    public async Task<IReadOnlyList<PresetProfile>> GetPresetsAsync(CancellationToken cancellationToken = default)
    {
        await _store.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _store.Gate.Release(); }
    }

    public async Task<PresetProfile> SavePresetAsync(PresetProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Name);
        using var settings = JsonDocument.Parse(profile.SettingsJson);
        await _store.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var presets = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            var saved = new PresetProfile(profile.Name, profile.Description, profile.SettingsJson, profile.IsDefault)
            {
                Id = string.IsNullOrWhiteSpace(profile.Id) ? Guid.NewGuid().ToString("N") : profile.Id,
                CreatedAt = profile.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            int index = presets.FindIndex(item => item.Id == saved.Id);
            if (index >= 0)
            {
                saved.CreatedAt = presets[index].CreatedAt;
                presets[index] = saved;
            }
            else { presets.Add(saved); }
            if (saved.IsDefault)
            {
                foreach (var item in presets)
                {
                    item.IsDefault = item.Id == saved.Id;
                }
            }
            await _store.WriteAsync(presets, cancellationToken).ConfigureAwait(false);
            return saved;
        }
        finally { _store.Gate.Release(); }
    }

    public async Task<bool> DeletePresetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _store.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var presets = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            bool removed = presets.RemoveAll(item => item.Id == id) > 0;
            if (removed) { await _store.WriteAsync(presets, cancellationToken).ConfigureAwait(false); }
            return removed;
        }
        finally { _store.Gate.Release(); }
    }

    public void Dispose() => _store.Dispose();
}
