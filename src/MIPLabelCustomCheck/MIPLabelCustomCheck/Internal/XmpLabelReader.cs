using System.Xml.Linq;

namespace MIPLabelCustomCheck.Internal;

internal static class XmpLabelReader
{
    internal static LabelPropertyParseResult Read(
        Stream stream,
        int maximumBytes,
        bool includeDetails = false)
    {
        var document = SafeXml.Load(stream, maximumBytes);
        return LabelPropertyParser.Parse(EnumerateProperties(document), includeDetails);
    }

    private static IEnumerable<KeyValuePair<string, string>> EnumerateProperties(
        XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                yield return KeyValuePair.Create(attribute.Name.LocalName, attribute.Value);
            }

            if (!element.HasElements)
            {
                yield return KeyValuePair.Create(element.Name.LocalName, element.Value);
            }
        }
    }
}
