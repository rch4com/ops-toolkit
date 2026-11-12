namespace MIPLabelCustomCheck.Internal;

internal static class FileFormatDetector
{
    private const int SignatureBufferSize = 1024;

    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> CompoundFileSignature =>
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static ReadOnlySpan<byte> ZipSignature => "PK\x03\x04"u8;

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> LittleEndianTiffSignature => [0x49, 0x49, 0x2A, 0x00];

    private static ReadOnlySpan<byte> BigEndianTiffSignature => [0x4D, 0x4D, 0x00, 0x2A];

    internal static FileFormat Detect(Stream stream, string? fileName = null)
    {
        var original = stream.Position;

        try
        {
            stream.Position = 0;
            Span<byte> buffer = stackalloc byte[SignatureBufferSize];
            var bytesRead = FillBuffer(stream, buffer[..PngSignature.Length]);
            var prefix = buffer[..bytesRead];

            if (prefix.StartsWith(".pfile"u8))
            {
                return FileFormat.GenericPFile;
            }

            if (prefix.StartsWith(PngSignature))
            {
                return FileFormat.Png;
            }

            if (prefix.StartsWith(CompoundFileSignature))
            {
                return FileFormat.CompoundFile;
            }

            if (prefix.StartsWith(ZipSignature))
            {
                return FileFormat.Zip;
            }

            if (prefix.StartsWith(JpegSignature))
            {
                return FileFormat.Jpeg;
            }

            if (prefix.StartsWith(LittleEndianTiffSignature) ||
                prefix.StartsWith(BigEndianTiffSignature))
            {
                return FileFormat.Tiff;
            }

            if (prefix.IndexOf("%PDF-"u8) >= 0)
            {
                return FileFormat.Pdf;
            }

            bytesRead += FillBuffer(stream, buffer[bytesRead..]);
            var signature = buffer[..bytesRead];
            if (signature.IndexOf("%PDF-"u8) >= 0)
            {
                return FileFormat.Pdf;
            }

            return HasExtension(fileName, ".eml") ? FileFormat.Eml : FileFormat.Unknown;
        }
        finally
        {
            stream.Position = original;
        }
    }

    private static bool HasExtension(string? fileName, string extension) =>
        !string.IsNullOrWhiteSpace(fileName) &&
        Path.GetExtension(fileName).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private static int FillBuffer(Stream stream, Span<byte> buffer)
    {
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = stream.Read(buffer[bytesRead..]);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        return bytesRead;
    }
}
