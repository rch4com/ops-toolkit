using OpsToolkit.Shared.Models;

namespace OpsToolkit.Shared.Services;

public sealed class RecentHistoryService(string filePath) : IDisposable
{
    private readonly JsonFileStore<RecentHistoryItem> _store = new(filePath);

    public async Task<IReadOnlyList<RecentHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _store.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await _store.ReadAsync(cancellationToken).ConfigureAwait(false); }
        finally { _store.Gate.Release(); }
    }

    public async Task AddItemAsync(string path, string actionType = "Process", bool isSuccess = true, string? note = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionType);
        await _store.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            path = path.Trim();
            var previous = items.Find(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            items.RemoveAll(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
            items.Insert(0, new RecentHistoryItem
            {
                Id = previous?.Id ?? Guid.NewGuid().ToString("N"),
                Path = path,
                DisplayName = string.IsNullOrEmpty(Path.GetFileName(path)) ? path : Path.GetFileName(path),
                Timestamp = DateTimeOffset.UtcNow,
                ActionType = actionType,
                IsSuccess = isSuccess,
                Note = note
            });
            if (items.Count > 10) { items.RemoveRange(10, items.Count - 10); }
            await _store.WriteAsync(items, cancellationToken).ConfigureAwait(false);
        }
        finally { _store.Gate.Release(); }
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _store.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _store.ReadAsync(cancellationToken).ConfigureAwait(false);
            await _store.WriteAsync([], cancellationToken).ConfigureAwait(false);
        }
        finally { _store.Gate.Release(); }
    }

    public void Dispose() => _store.Dispose();
}
