namespace MIPLabelCustomCheck.Internal;

/// <summary>
/// Shared boundary guard for declared and decompressed lengths read from untrusted input. Readers
/// use this instead of duplicating an ad hoc comparison plus an unchecked (or separately
/// <c>checked</c>) cast, so a declared size that is negative, that exceeds the caller's configured
/// maximum, or that would not fit in an <see cref="int"/> is rejected uniformly as an
/// <see cref="InvalidDataException"/> before any buffer sized by it is allocated.
/// </summary>
internal static class InputGuard
{
    internal static int CheckedLength(long value, int maximum, string field)
    {
        if (value < 0 || value > maximum)
        {
            throw new InvalidDataException($"{field} exceeds the supported limit.");
        }

        return checked((int)value);
    }
}
