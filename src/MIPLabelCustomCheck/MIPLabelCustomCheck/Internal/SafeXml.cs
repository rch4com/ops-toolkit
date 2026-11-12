using System.Xml;
using System.Xml.Linq;

namespace MIPLabelCustomCheck.Internal;

internal static class SafeXml
{
    internal static XDocument Load(Stream stream, int maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersFromEntities = 0,
            CloseInput = false
        };

        using var limitedStream = new LimitedReadStream(stream, maximumBytes);
        using var reader = XmlReader.Create(limitedStream, settings);
        return XDocument.Load(reader);
    }
}
