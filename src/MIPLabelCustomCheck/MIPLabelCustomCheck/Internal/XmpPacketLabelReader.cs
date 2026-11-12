using System.Text;
using System.Xml;

namespace MIPLabelCustomCheck.Internal;

internal static class XmpPacketLabelReader
{
    internal static void AddLabels(
        byte[] xmpBytes,
        bool includeDetails,
        List<Guid> labelIds,
        HashSet<Guid> seenLabelIds,
        List<LabelDetail>? details)
    {
        using var xmpStream = new MemoryStream(xmpBytes, writable: false);
        LabelPropertyParseResult parsed;
        try
        {
            parsed = XmpLabelReader.Read(xmpStream, xmpBytes.Length, includeDetails);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or XmlException or DecoderFallbackException)
        {
            throw new InvalidXmpPacketException("The XMP metadata is invalid.", exception);
        }

        if (parsed.InvalidMetadata)
        {
            throw new InvalidXmpPacketException("The XMP label metadata is invalid.");
        }

        foreach (var labelId in parsed.LabelIds)
        {
            if (!seenLabelIds.Add(labelId))
            {
                continue;
            }

            labelIds.Add(labelId);
            if (details is not null &&
                parsed.Details.FirstOrDefault(d => d.Id == labelId) is { } detail)
            {
                details.Add(detail);
            }
        }
    }
}

internal sealed class InvalidXmpPacketException : Exception
{
    internal InvalidXmpPacketException(string message)
        : base(message)
    {
    }

    internal InvalidXmpPacketException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
