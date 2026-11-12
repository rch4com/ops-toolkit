using MIPLabelCustomCheck.Internal;

namespace MIPLabelCustomCheck;

/// <summary>
/// Detects Microsoft Purview sensitivity-label GUIDs in Office, PDF, and PNG files by reading
/// their metadata directly, without depending on the MIP SDK or any Microsoft Purview runtime.
/// </summary>
/// <remarks>
/// This type is stateless and holds no per-call mutable state, so a single instance is safe to
/// use concurrently from multiple threads as long as each concurrent call targets an independent
/// file path or stream; see the "Thread safety" section of the project README for details.
/// </remarks>
public sealed class MipLabelChecker
{
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;

    /// <summary>
    /// Opens the file at <paramref name="filePath"/> for shared reading and inspects it for
    /// Microsoft Purview sensitivity-label metadata.
    /// </summary>
    /// <param name="filePath">The path of the file to inspect.</param>
    /// <param name="options">
    /// What to read. Defaults to <see cref="LabelCheckOptions.Default"/>, which reads label GUIDs
    /// only; pass <see cref="LabelCheckOptions.WithDetails"/> to also populate
    /// <see cref="LabelCheckResult.Details"/>.
    /// </param>
    /// <returns>The detected label status, label GUIDs, and protection state.</returns>
    /// <remarks>
    /// The file is opened with <see cref="FileShare.ReadWrite"/> combined with
    /// <see cref="FileShare.Delete"/>, so other processes may continue to read, write, or delete
    /// the file while it is being inspected. A file that changes concurrently may therefore be
    /// reported as <see cref="LabelStatus.InvalidFile"/>, or its read may fail with
    /// <see cref="IOException"/>; callers scanning live or remote locations should be prepared
    /// to retry. If the file is held with an exclusive lock (Windows
    /// sharing violation or lock violation) this method throws
    /// <see cref="MipLabelCheckException"/> with <see cref="MipLabelCheckException.Reason"/> set
    /// to <see cref="MipLabelCheckErrorReason.FileLocked"/> instead of the underlying
    /// <see cref="IOException"/>.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is null, empty, or
    /// consists only of white space.</exception>
    /// <exception cref="System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="System.IO.DirectoryNotFoundException">Part of the directory path does
    /// not exist.</exception>
    /// <exception cref="System.UnauthorizedAccessException">The caller lacks read permission, or
    /// <paramref name="filePath"/> refers to a directory rather than a file.</exception>
    /// <exception cref="System.IO.PathTooLongException">The path exceeds the system-defined
    /// maximum length.</exception>
    /// <exception cref="System.IO.IOException">The file could not be read (for example, it was
    /// modified or truncated by another process while being inspected, or the underlying storage
    /// failed). Exclusively-locked files are reported as <see cref="MipLabelCheckException"/>
    /// instead — see below.</exception>
    /// <exception cref="MipLabelCheckException">The file could not be opened because it is
    /// exclusively locked by another process.</exception>
    public LabelCheckResult Check(string filePath, LabelCheckOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var streamOptions = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            BufferSize = 4096,
            Options = FileOptions.RandomAccess
        };

        try
        {
            using var stream = new FileStream(filePath, streamOptions);
            return Check(stream, filePath, options);
        }
        catch (IOException exception) when (
            OperatingSystem.IsWindows() && IsSharingViolation(exception.HResult))
        {
            throw new MipLabelCheckException(MipLabelCheckErrorReason.FileLocked, filePath, exception);
        }
    }

    /// <summary>
    /// Asynchronously inspects the file at <paramref name="filePath"/> for Microsoft Purview
    /// sensitivity-label metadata.
    /// </summary>
    /// <param name="filePath">The path of the file to inspect.</param>
    /// <param name="options">
    /// What to read. Defaults to <see cref="LabelCheckOptions.Default"/>, which reads label GUIDs
    /// only; pass <see cref="LabelCheckOptions.WithDetails"/> to also populate
    /// <see cref="LabelCheckResult.Details"/>.
    /// </param>
    /// <param name="cancellationToken">A token that cooperatively cancels the check.</param>
    /// <returns>
    /// A task that completes with the detected label status, label GUIDs, and protection state.
    /// </returns>
    /// <remarks>
    /// The same file-sharing and thread-safety contract as <see cref="Check(string, LabelCheckOptions?)"/>
    /// applies. This type remains stateless and safe to use concurrently from multiple threads as
    /// long as each concurrent call targets an independent file path or stream.
    ///
    /// Cancellation is cooperative. If <paramref name="cancellationToken"/> is canceled before
    /// inspection starts, the file is not opened and the returned task is canceled. The current
    /// format readers are synchronous; once file opening or a reader operation has started,
    /// cancellation does not abort that in-flight I/O. A cancellation request made during the
    /// synchronous inspection is observed only after that inspection returns to this method.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is null, empty, or
    /// consists only of white space.</exception>
    /// <exception cref="System.IO.FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="System.IO.DirectoryNotFoundException">Part of the directory path does
    /// not exist.</exception>
    /// <exception cref="System.UnauthorizedAccessException">The caller lacks read permission, or
    /// <paramref name="filePath"/> refers to a directory rather than a file.</exception>
    /// <exception cref="System.IO.PathTooLongException">The path exceeds the system-defined
    /// maximum length.</exception>
    /// <exception cref="System.IO.IOException">The file could not be read (for example, it was
    /// modified or truncated by another process while being inspected, or the underlying storage
    /// failed). Exclusively-locked files are reported as <see cref="MipLabelCheckException"/>
    /// instead — see below.</exception>
    /// <exception cref="MipLabelCheckException">The file could not be opened because it is
    /// exclusively locked by another process.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was
    /// canceled before inspection started or at a cooperative cancellation point.</exception>
    public Task<LabelCheckResult> CheckAsync(
        string filePath,
        LabelCheckOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<LabelCheckResult>(cancellationToken);
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = Check(filePath, options);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            },
            cancellationToken);
    }

    /// <summary>
    /// Inspects <paramref name="stream"/> for Microsoft Purview sensitivity-label metadata.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned anywhere; its position is
    /// restored before this method returns, whether it succeeds or throws.</param>
    /// <param name="fileName">Reserved for future use; currently ignored. Pass the original file
    /// name or path when available, in case a future version uses it (for example, as an
    /// extension-based hint).</param>
    /// <param name="options">
    /// What to read. Defaults to <see cref="LabelCheckOptions.Default"/>, which reads label GUIDs
    /// only; pass <see cref="LabelCheckOptions.WithDetails"/> to also populate
    /// <see cref="LabelCheckResult.Details"/>.
    /// </param>
    /// <returns>The detected label status, label GUIDs, and protection state.</returns>
    /// <remarks>
    /// The file format is determined from the stream's own content signature, not from
    /// <paramref name="fileName"/>. This overload does not open, lock, or otherwise take
    /// ownership of <paramref name="stream"/>; the caller remains responsible for disposing it.
    /// Because this method mutates and then restores <paramref name="stream"/>'s
    /// <see cref="Stream.Position"/>, concurrent calls must not share the same stream instance.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
    /// <exception cref="NotSupportedException"><paramref name="stream"/> is not seekable.</exception>
    public LabelCheckResult Check(
        Stream stream,
        string? fileName = null,
        LabelCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new NotSupportedException("The stream must be seekable.");
        }

        _ = fileName;
        var includeDetails = (options ?? LabelCheckOptions.Default).IncludeDetails;
        var original = stream.Position;

        try
        {
            stream.Position = 0;
            var result = FileFormatDetector.Detect(stream, fileName) switch
            {
                FileFormat.GenericPFile => PFileProtectionReader.Read(),
                FileFormat.Png => PngLabelReader.Read(stream, includeDetails),
                FileFormat.Jpeg => JpegLabelReader.Read(stream, includeDetails),
                FileFormat.Tiff => TiffLabelReader.Read(stream, includeDetails),
                FileFormat.Zip => OpenXmlLabelReader.Read(stream, includeDetails),
                FileFormat.CompoundFile => ReadCompoundFile(stream, includeDetails),
                FileFormat.Eml => EmlLabelReader.Read(stream, includeDetails),
                FileFormat.Pdf => PdfLabelReader.Read(stream, includeDetails),
                _ => LabelReadResult.Unsupported()
            };

            return result.ToPublic();
        }
        finally
        {
            stream.Position = original;
        }
    }

    private static LabelReadResult ReadCompoundFile(Stream stream, bool includeDetails)
    {
        var messageResult = MsgLabelReader.Read(stream, includeDetails);
        if (messageResult.Status == LabelStatus.Labeled)
        {
            return messageResult;
        }

        stream.Position = 0;
        return CompoundFileLabelReader.Read(stream, includeDetails);
    }

    /// <summary>
    /// Asynchronously inspects <paramref name="stream"/> for Microsoft Purview
    /// sensitivity-label metadata.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned anywhere; its position is
    /// restored before the returned task completes, whether it succeeds or throws.</param>
    /// <param name="fileName">Reserved for future use; currently ignored. Pass the original file
    /// name or path when available, in case a future version uses it (for example, as an
    /// extension-based hint).</param>
    /// <param name="options">
    /// What to read. Defaults to <see cref="LabelCheckOptions.Default"/>, which reads label GUIDs
    /// only; pass <see cref="LabelCheckOptions.WithDetails"/> to also populate
    /// <see cref="LabelCheckResult.Details"/>.
    /// </param>
    /// <param name="cancellationToken">A token that cooperatively cancels the check.</param>
    /// <returns>
    /// A task that completes with the detected label status, label GUIDs, and protection state.
    /// </returns>
    /// <remarks>
    /// The file format is determined from the stream's own content signature, not from
    /// <paramref name="fileName"/>. This overload does not open, lock, or otherwise take
    /// ownership of <paramref name="stream"/>; the caller remains responsible for disposing it
    /// after the returned task completes. Because this method mutates and then restores
    /// <paramref name="stream"/>'s <see cref="Stream.Position"/>, concurrent calls must not share
    /// the same stream instance, and the caller must not otherwise use or dispose the stream until
    /// the returned task completes.
    ///
    /// Cancellation is cooperative. If <paramref name="cancellationToken"/> is canceled before
    /// inspection starts, the stream position is not changed and the returned task is canceled. The
    /// current format readers are synchronous; once a reader operation has started, cancellation
    /// does not abort that in-flight I/O. A cancellation request made during the synchronous
    /// inspection is observed only after that inspection returns to this method, after the stream
    /// position has been restored.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is not readable.</exception>
    /// <exception cref="NotSupportedException"><paramref name="stream"/> is not seekable.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was
    /// canceled before inspection started or at a cooperative cancellation point.</exception>
    public Task<LabelCheckResult> CheckAsync(
        Stream stream,
        string? fileName = null,
        LabelCheckOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new NotSupportedException("The stream must be seekable.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<LabelCheckResult>(cancellationToken);
        }

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = Check(stream, fileName, options);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            },
            cancellationToken);
    }

    /// <summary>Internal for direct unit testing of the Win32 HResult classification logic.</summary>
    internal static bool IsSharingViolation(int hResult)
    {
        var win32Error = hResult & 0xFFFF;
        return win32Error == ErrorSharingViolation || win32Error == ErrorLockViolation;
    }
}
