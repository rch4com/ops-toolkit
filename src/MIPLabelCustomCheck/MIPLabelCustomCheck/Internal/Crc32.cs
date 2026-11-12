namespace MIPLabelCustomCheck.Internal;

internal static class Crc32
{
    private const uint Polynomial = 0xEDB88320u;

    internal static uint Compute(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        Update(ref crc, bytes);
        return ~crc;
    }

    internal static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xFFFFFFFFu;
        Update(ref crc, first);
        Update(ref crc, second);
        return ~crc;
    }

    private static void Update(ref uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : Polynomial);
            }
        }
    }
}
