using System.Globalization;
using System.Xml.Linq;

namespace MIPLabelCustomCheck.Internal;

internal sealed record LabelInfoRecord(
    Guid LabelId,
    Guid? SiteId,
    bool Enabled,
    bool Removed)
{
    /// <summary>From the <c>method</c> attribute. <see cref="LabelMethod.Unspecified"/> when absent.</summary>
    internal LabelMethod Method { get; init; }

    /// <summary>From the <c>contentBits</c> attribute; null when absent or unparseable.</summary>
    internal int? ContentBits { get; init; }

    /// <summary>From the <c>name</c> attribute, which newer clients write; null when absent.</summary>
    internal string? Name { get; init; }

    /// <summary>From the <c>setDate</c> attribute; null when absent or unparseable.</summary>
    internal DateTimeOffset? SetDate { get; init; }

    /// <summary>From the <c>actionId</c> attribute; null when absent or unparseable.</summary>
    internal Guid? ActionId { get; init; }

    internal LabelDetail ToDetail() => new()
    {
        Id = LabelId,
        SiteId = SiteId,
        Name = Name,
        Method = Method,
        ContentBits = ContentBits,
        ActionId = ActionId,
        SetDate = SetDate
    };
}

internal sealed record LabelInfoReadResult(
    IReadOnlyList<LabelInfoRecord> Records,
    bool InvalidMetadata);

internal static class LabelInfoXmlReader
{
    private static readonly XNamespace LabelNamespace =
        "http://schemas.microsoft.com/office/2020/mipLabelMetadata";

    internal static LabelInfoReadResult Read(Stream stream, int maximumBytes)
    {
        var document = SafeXml.Load(stream, maximumBytes);
        var records = new List<LabelInfoRecord>();
        var invalidMetadata = false;

        // §8.2 of the design spec documents labelList/label: labels are direct children of the
        // root element, not arbitrarily nested descendants.
        foreach (var element in document.Root?.Elements(LabelNamespace + "label") ?? [])
        {
            if (!Guid.TryParse(element.Attribute("id")?.Value, out var labelId))
            {
                invalidMetadata = true;
                continue;
            }

            Guid? siteId = null;
            var siteIdAttribute = element.Attribute("siteId");
            if (siteIdAttribute is not null)
            {
                if (!Guid.TryParse(siteIdAttribute.Value, out var parsedSiteId))
                {
                    invalidMetadata = true;
                    continue;
                }

                siteId = parsedSiteId;
            }

            if (!TryReadBoolean(element, "enabled", out var enabled) ||
                !TryReadBoolean(element, "removed", out var removed))
            {
                invalidMetadata = true;
                continue;
            }

            records.Add(new LabelInfoRecord(labelId, siteId, enabled, removed)
            {
                // 부가 속성은 값이 이상해도 InvalidMetadata로 올리지 않고 버린다. 레이블
                // 자체는 멀쩡히 존재하므로, 예컨대 setDate가 깨졌다는 이유로 "레이블 없음"이라
                // 답하면 사소한 흠을 틀린 답으로 바꾸는 셈이 된다.
                Method = ParseMethod(element.Attribute("method")?.Value),
                ContentBits = ParseInt(element.Attribute("contentBits")?.Value),
                Name = NullIfEmpty(element.Attribute("name")?.Value),
                SetDate = ParseDate(element.Attribute("setDate")?.Value),
                ActionId = ParseGuid(element.Attribute("actionId")?.Value)
            });
        }

        return new LabelInfoReadResult(records, invalidMetadata);
    }

    private static LabelMethod ParseMethod(string? value) => value switch
    {
        null => LabelMethod.Unspecified,
        _ when value.Equals("Privileged", StringComparison.OrdinalIgnoreCase) => LabelMethod.Privileged,
        _ when value.Equals("Standard", StringComparison.OrdinalIgnoreCase) => LabelMethod.Standard,
        _ => LabelMethod.Unspecified
    };

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static bool TryReadBoolean(XElement element, string attributeName, out bool value)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute is null)
        {
            value = false;
            return true;
        }

        if (attribute.Value == "1" ||
            attribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (attribute.Value == "0" ||
            attribute.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
}
