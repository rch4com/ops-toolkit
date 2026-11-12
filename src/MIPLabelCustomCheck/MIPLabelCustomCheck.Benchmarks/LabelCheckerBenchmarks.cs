using System.Buffers.Binary;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Microsoft.Win32.SafeHandles;
using MIPLabelCustomCheck;

namespace MIPLabelCustomCheck.Benchmarks;

/// <summary>
/// Single-file and caller-side-parallel benchmarks for <see cref="MipLabelChecker"/> against the
/// repository's real fixture files (one per supported real-world format) and a synthetic 512 MiB
/// unlabeled PNG whose only well-formed metadata sits at the very beginning and very end of the
/// file, with a large unread gap in between. The large fixture exists to reveal any reader that
/// scales its work - or, worse, its managed allocations - with file size instead of with the
/// amount of metadata actually present.
/// </summary>
[MemoryDiagnoser]
public class LabelCheckerBenchmarks
{
    private const long LargeUnlabeledFileLength = 512L * 1024 * 1024;

    private readonly MipLabelChecker _checker = new();

    private string _docxPath = string.Empty;
    private string _xlsxPath = string.Empty;
    private string _pptxPath = string.Empty;
    private string _docPath = string.Empty;
    private string _xlsPath = string.Empty;
    private string _pptPath = string.Empty;
    private string _pdfPath = string.Empty;
    private string _pngPath = string.Empty;
    private string _largeUnlabeledPath = string.Empty;
    private string[] _allFixturePaths = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        var repositoryRoot = FindRepositoryRoot();
        _docxPath = ResolveFixture(repositoryRoot, ".docx");
        _xlsxPath = ResolveFixture(repositoryRoot, ".xlsx");
        _pptxPath = ResolveFixture(repositoryRoot, ".pptx");
        _docPath = ResolveFixture(repositoryRoot, ".doc");
        _xlsPath = ResolveFixture(repositoryRoot, ".xls");
        _pptPath = ResolveFixture(repositoryRoot, ".ppt");
        _pdfPath = ResolveFixture(repositoryRoot, ".pdf");
        _pngPath = ResolveFixture(repositoryRoot, ".png");

        _allFixturePaths =
        [
            _docxPath, _xlsxPath, _pptxPath,
            _docPath, _xlsPath, _pptPath,
            _pdfPath, _pngPath
        ];

        _largeUnlabeledPath = Path.Combine(
            Path.GetTempPath(),
            $"mip-label-benchmark-{Guid.NewGuid():N}.png");
        LargeSparsePngFile.Create(_largeUnlabeledPath, LargeUnlabeledFileLength);
    }

    /// <summary>Deletes only the synthetic large file this benchmark class created; every other
    /// path resolved in <see cref="GlobalSetup"/> is a real repository fixture and must survive.</summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_largeUnlabeledPath.Length > 0 && File.Exists(_largeUnlabeledPath))
        {
            File.Delete(_largeUnlabeledPath);
        }
    }

    [Benchmark]
    public LabelCheckResult CheckDocxSingle() => _checker.Check(_docxPath);

    [Benchmark]
    public LabelCheckResult CheckXlsxSingle() => _checker.Check(_xlsxPath);

    [Benchmark]
    public LabelCheckResult CheckPptxSingle() => _checker.Check(_pptxPath);

    [Benchmark]
    public LabelCheckResult CheckDocSingle() => _checker.Check(_docPath);

    [Benchmark]
    public LabelCheckResult CheckXlsSingle() => _checker.Check(_xlsPath);

    [Benchmark]
    public LabelCheckResult CheckPptSingle() => _checker.Check(_pptPath);

    [Benchmark]
    public LabelCheckResult CheckPdfSingle() => _checker.Check(_pdfPath);

    [Benchmark]
    public LabelCheckResult CheckPngSingle() => _checker.Check(_pngPath);

    /// <summary>Benchmarks a single check against the synthetic 512 MiB unlabeled file, to
    /// surface any reader whose time or allocations scale with file size rather than with
    /// metadata size.</summary>
    [Benchmark]
    public LabelCheckResult CheckLargeUnlabeledSingle() => _checker.Check(_largeUnlabeledPath);

    /// <summary>Caller-owned parallel check of every real fixture, sharing one
    /// <see cref="MipLabelChecker"/> instance across threads - the pattern documented in the
    /// project README.</summary>
    [Benchmark]
    public LabelCheckResult[] CheckAllFixturesParallel()
    {
        var results = new LabelCheckResult[_allFixturePaths.Length];
        Parallel.For(0, _allFixturePaths.Length, index =>
        {
            results[index] = _checker.Check(_allFixturePaths[index]);
        });

        return results;
    }

    private static string ResolveFixture(string repositoryRoot, string extension)
    {
        string inFixturesSubdir = Path.Combine(repositoryRoot, "Fixtures", $"MIP Label{extension}");
        return File.Exists(inFixturesSubdir)
            ? inFixturesSubdir
            : Path.Combine(repositoryRoot, $"MIP Label{extension}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MIPLabelCustomCheck.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate MIPLabelCustomCheck.sln above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Builds a synthetic, large, unlabeled PNG file with well-formed metadata only at the
    /// beginning (signature + IHDR) and the end (IEND), and a huge unread ancillary chunk in
    /// between whose payload is never written to disk - it is left as a sparse hole, so the file
    /// reports a 512 MiB length while consuming only a few kilobytes of real disk space (on file
    /// systems that support sparse allocation; elsewhere the payload region is still never read,
    /// only allocated). <see cref="MIPLabelCustomCheck.Internal.PngLabelReader"/> does not
    /// validate the CRC of non-<c>iTXt</c> chunks, so the placeholder CRC fields below never need
    /// to hold a value computed from real payload bytes.
    /// </summary>
    private static class LargeSparsePngFile
    {
        private static ReadOnlySpan<byte> Signature =>
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        private const int ChunkHeaderLength = 8;
        private const int ChunkCrcLength = 4;
        private const int IhdrPayloadLength = 13;

        internal static void Create(string path, long totalLength)
        {
            var ihdrChunkLength = ChunkHeaderLength + IhdrPayloadLength + ChunkCrcLength;
            var iendChunkLength = ChunkHeaderLength + ChunkCrcLength;
            var overhead = Signature.Length + ihdrChunkLength + ChunkHeaderLength + ChunkCrcLength +
                iendChunkLength;
            var junkPayloadLength = totalLength - overhead;

            if (junkPayloadLength < 0 || junkPayloadLength > uint.MaxValue)
            {
                throw new InvalidOperationException(
                    $"{totalLength:N0} bytes is not a valid target size for the benchmark file.");
            }

            using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None);

            TryMarkSparse(stream);
            stream.SetLength(totalLength);

            stream.Position = 0;
            stream.Write(Signature);
            WriteChunkHeader(stream, IhdrPayloadLength, "IHDR"u8);
            stream.Write(new byte[IhdrPayloadLength]);
            stream.Write(new byte[ChunkCrcLength]);

            WriteChunkHeader(stream, (uint)junkPayloadLength, "juNK"u8);
            stream.Position += junkPayloadLength; // Left unwritten: a sparse hole, not real data.
            stream.Write(new byte[ChunkCrcLength]);

            WriteChunkHeader(stream, 0, "IEND"u8);
            stream.Write(new byte[ChunkCrcLength]);
        }

        private static void WriteChunkHeader(Stream stream, uint payloadLength, ReadOnlySpan<byte> type)
        {
            Span<byte> header = stackalloc byte[ChunkHeaderLength];
            BinaryPrimitives.WriteUInt32BigEndian(header[..4], payloadLength);
            type.CopyTo(header[4..]);
            stream.Write(header);
        }

        private static void TryMarkSparse(FileStream stream)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            // Best-effort: if the volume or driver does not support sparse files, the ioctl
            // fails and the benchmark file simply consumes its full 512 MiB on disk instead of a
            // few kilobytes. Either way, the payload region below is never written or read.
            _ = SparseFileNativeMethods.DeviceIoControl(
                stream.SafeFileHandle,
                SparseFileNativeMethods.FsctlSetSparse,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                0,
                out _,
                IntPtr.Zero);
        }
    }
}

/// <summary>
/// Win32 interop for marking a file sparse (<c>FSCTL_SET_SPARSE</c>) before extending it, used
/// only by <see cref="LabelCheckerBenchmarks"/>'s large-file benchmark setup.
/// </summary>
internal static class SparseFileNativeMethods
{
    internal const uint FsctlSetSparse = 0x000900C4;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        IntPtr inBuffer,
        uint inBufferSize,
        IntPtr outBuffer,
        uint outBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}
