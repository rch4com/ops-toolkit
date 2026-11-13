using System.Runtime.CompilerServices;
using System.Text;

namespace OpsToolkit.Shared.Services;

public sealed class LogTailService
{
    public async IAsyncEnumerable<string> ReadLinesAsync(string path, int initialReadLines = 100, TimeSpan? pollingInterval = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(initialReadLines);
        var interval = pollingInterval ?? TimeSpan.FromSeconds(1);
        if (interval <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(pollingInterval)); }
        path = Path.GetFullPath(path);
        long offset = 0;
        byte[] fingerprint = [];
        var pending = new List<byte>();
        var initial = new Queue<string>();
        bool first = true;
        bool atFileStart = true;
        var buffer = new byte[4096];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var appended = new List<string>();
            // Reopening with Delete sharing follows a rotated file at the original path.
            await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous))
            {
                bool reset = stream.Length < offset;
                if (!reset && offset > 0)
                {
                    var currentFingerprint = await FingerprintAsync(stream, offset, cancellationToken).ConfigureAwait(false);
                    reset = !fingerprint.AsSpan().SequenceEqual(currentFingerprint);
                }
                if (reset)
                {
                    offset = 0;
                    pending.Clear();
                    atFileStart = true;
                }
                stream.Position = offset;
                int count;
                while ((count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        byte value = buffer[i];
                        if (value == (byte)'\n')
                        {
                            if (pending.Count > 0 && pending[^1] == (byte)'\r') { pending.RemoveAt(pending.Count - 1); }
                            var line = Encoding.UTF8.GetString(pending.ToArray());
                            if (atFileStart) { line = line.TrimStart('\uFEFF'); }
                            atFileStart = false;
                            pending.Clear();
                            if (first)
                            {
                                initial.Enqueue(line);
                                while (initial.Count > initialReadLines) { initial.Dequeue(); }
                            }
                            else { appended.Add(line); }
                        }
                        else { pending.Add(value); }
                    }
                    offset = stream.Position;
                }
                fingerprint = await FingerprintAsync(stream, offset, cancellationToken).ConfigureAwait(false);
            }
            foreach (var line in appended)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
            }
            if (first)
            {
                first = false;
                foreach (var line in initial)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return line;
                }
                initial.Clear();
            }
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> FingerprintAsync(FileStream stream, long offset, CancellationToken cancellationToken)
    {
        // shortcut: sample the consumed prefix and tail; use file identity/full hashing if middle-only edits must be detected.
        if (stream.Length < offset) { return []; }
        int size = (int)Math.Min(offset, 128);
        byte[] bytes = new byte[size * 2];
        stream.Position = 0;
        int headerRead = await stream.ReadAtLeastAsync(bytes.AsMemory(0, size), size, false, cancellationToken).ConfigureAwait(false);
        stream.Position = Math.Max(0, offset - size);
        int tailRead = await stream.ReadAtLeastAsync(bytes.AsMemory(size, size), size, false, cancellationToken).ConfigureAwait(false);
        return headerRead == size && tailRead == size ? bytes : [];
    }
}
