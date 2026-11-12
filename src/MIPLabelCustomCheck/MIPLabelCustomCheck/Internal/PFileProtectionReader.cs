namespace MIPLabelCustomCheck.Internal;

internal static class PFileProtectionReader
{
    internal static LabelReadResult Read() =>
        LabelReadResult.ProtectedUnknown(ProtectionType.GenericPFile);
}
