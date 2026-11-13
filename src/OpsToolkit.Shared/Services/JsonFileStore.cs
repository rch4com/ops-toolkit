using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpsToolkit.Shared.Services;

internal sealed class JsonFileStore<T> : IDisposable where T : class
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly string _filePath;
    // shortcut: one owner per store file; add cross-process locking if shared writers are required.
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    internal JsonFileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    internal async Task<List<T>> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, Options, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("The store must contain a JSON array.");
            if (items.Any(item => item is null))
            {
                throw new JsonException("Store entries must not be null.");
            }
            return items;
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    internal async Task WriteAsync(List<T> items, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, items, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public void Dispose() => Gate.Dispose();
}
