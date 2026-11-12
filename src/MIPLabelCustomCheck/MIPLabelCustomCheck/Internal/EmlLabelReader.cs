using System.Text;

namespace MIPLabelCustomCheck.Internal;

internal static class EmlLabelReader
{
    private const int MaxHeaderBytes = 1024 * 1024;
    private const string SerializedLabelsHeader = "msip_labels";

    internal static LabelReadResult Read(Stream stream, bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var headerText = ReadHeaderText(stream);
            if (headerText is null)
            {
                return LabelReadResult.Unlabeled();
            }

            var properties = new List<KeyValuePair<string, string>>();
            foreach (var (name, value) in ReadHeaders(headerText))
            {
                // Microsoft MIP SDK label-metadata docs state that email serializes the
                // MSIP_Label_* key/value set into a single MSIP_Labels header, delimited by
                // semicolon+space. [MS-OXPROPS] PidNameMSIPLabels likewise defines the
                // msip_labels internet-header named property for email.
                if (name.Equals(SerializedLabelsHeader, StringComparison.OrdinalIgnoreCase))
                {
                    properties.AddRange(ParseSerializedProperties(value));
                    continue;
                }

                // No equally authoritative source was found for one-header-per-property EML
                // storage. Keep this as a provisional conservative scan: it still requires the
                // exact MSIP_Label_{guid}_Enabled key shape and an active value.
                properties.Add(new KeyValuePair<string, string>(name, value.Trim()));
            }

            return FromProperties(properties, includeDetails);
        }
        catch (DecoderFallbackException)
        {
            return LabelReadResult.Invalid();
        }
        catch (InvalidDataException)
        {
            return LabelReadResult.Invalid();
        }
    }

    internal static LabelReadResult FromProperties(
        IEnumerable<KeyValuePair<string, string>> properties,
        bool includeDetails,
        LabelSource source = LabelSource.EmailHeader)
    {
        var parsed = LabelPropertyParser.Parse(properties, includeDetails);
        if (parsed.InvalidMetadata)
        {
            return LabelReadResult.Invalid();
        }

        return parsed.LabelIds.Count == 0
            ? LabelReadResult.Unlabeled()
            : LabelReadResult.Labeled(
                parsed.LabelIds,
                ProtectionType.None,
                parsed.Details,
                Enumerable.Repeat(source, parsed.LabelIds.Count).ToArray());
    }

    internal static IEnumerable<KeyValuePair<string, string>> ParseSerializedProperties(string value)
    {
        foreach (var part in value.Split(';'))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            var propertyValue = part[(separator + 1)..].Trim();
            if (key.Length > 0)
            {
                yield return new KeyValuePair<string, string>(key, propertyValue);
            }
        }
    }

    private static string? ReadHeaderText(Stream stream)
    {
        stream.Position = 0;
        var maximum = InputGuard.CheckedLength(
            Math.Min(stream.Length, MaxHeaderBytes + 4L),
            MaxHeaderBytes + 4,
            "The email header");
        var buffer = new byte[maximum];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer.AsSpan(total));
            if (read == 0)
            {
                break;
            }

            total += read;
            var headerEnd = FindHeaderEnd(buffer.AsSpan(0, total));
            if (headerEnd >= 0)
            {
                return Encoding.UTF8.GetString(buffer, 0, headerEnd);
            }
        }

        if (stream.Length > MaxHeaderBytes)
        {
            throw new InvalidDataException("The email header exceeds the byte limit.");
        }

        return total == 0 ? null : Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static int FindHeaderEnd(ReadOnlySpan<byte> bytes)
    {
        var crlf = bytes.IndexOf("\r\n\r\n"u8);
        if (crlf >= 0)
        {
            return crlf;
        }

        var lf = bytes.IndexOf("\n\n"u8);
        return lf >= 0 ? lf : -1;
    }

    private static IEnumerable<(string Name, string Value)> ReadHeaders(string headerText)
    {
        string? currentName = null;
        var currentValue = new StringBuilder();

        foreach (var rawLine in headerText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (rawLine.Length > 0 && (rawLine[0] == ' ' || rawLine[0] == '\t'))
            {
                if (currentName is not null)
                {
                    currentValue.Append(' ');
                    currentValue.Append(rawLine.Trim());
                }

                continue;
            }

            if (currentName is not null)
            {
                yield return (currentName, currentValue.ToString());
            }

            var separator = rawLine.IndexOf(':');
            if (separator <= 0)
            {
                currentName = null;
                currentValue.Clear();
                continue;
            }

            currentName = rawLine[..separator].Trim();
            currentValue.Clear();
            currentValue.Append(rawLine[(separator + 1)..].Trim());
        }

        if (currentName is not null)
        {
            yield return (currentName, currentValue.ToString());
        }
    }
}
