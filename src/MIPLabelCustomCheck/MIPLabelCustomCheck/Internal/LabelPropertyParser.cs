using System.Globalization;

namespace MIPLabelCustomCheck.Internal;

internal sealed record LabelPropertyParseResult(
    IReadOnlyList<Guid> LabelIds,
    bool InvalidMetadata,
    IReadOnlyList<LabelDetail> Details);

internal static class LabelPropertyParser
{
    private const string Prefix = "MSIP_Label_";
    private const string Suffix = "_Enabled";

    internal static LabelPropertyParseResult Parse(
        IEnumerable<KeyValuePair<string, string>> properties,
        bool includeDetails = false)
    {
        ArgumentNullException.ThrowIfNull(properties);

        // 속성 열거는 한 번만 한다. 호출자가 넘긴 것이 지연 열거일 수 있고,
        // 부가 정보를 모으려면 같은 목록을 두 번 훑어야 하기 때문이다.
        var materialized = properties as IReadOnlyList<KeyValuePair<string, string>> ??
            properties.ToArray();

        var labelIds = new List<Guid>();
        var seenLabelIds = new HashSet<Guid>();
        var invalidMetadata = false;

        foreach (var property in materialized)
        {
            if (!property.Key.StartsWith(Prefix, StringComparison.Ordinal) ||
                !property.Key.EndsWith(Suffix, StringComparison.Ordinal) ||
                !IsActive(property.Value))
            {
                continue;
            }

            var guidText = property.Key[Prefix.Length..^Suffix.Length];
            if (!Guid.TryParse(guidText, out var labelId))
            {
                invalidMetadata = true;
                continue;
            }

            if (seenLabelIds.Add(labelId))
            {
                labelIds.Add(labelId);
            }
        }

        var details = includeDetails && labelIds.Count > 0
            ? CollectDetails(materialized, labelIds)
            : [];

        return new LabelPropertyParseResult(labelIds, invalidMetadata, details);
    }

    /// <summary>
    /// Gathers the <c>MSIP_Label_{guid}_{attribute}</c> properties for labels already known to be
    /// active, in the same order as <paramref name="labelIds"/>.
    /// </summary>
    /// <remarks>
    /// A value that will not parse is dropped rather than reported as invalid metadata: the label
    /// itself is still legitimately present, and refusing to report it because, say, its SetDate
    /// is malformed would turn a cosmetic problem into a wrong answer about whether the file is
    /// labelled.
    /// </remarks>
    private static IReadOnlyList<LabelDetail> CollectDetails(
        IReadOnlyList<KeyValuePair<string, string>> properties,
        IReadOnlyList<Guid> labelIds)
    {
        var builders = new Dictionary<Guid, DetailBuilder>();
        foreach (var labelId in labelIds)
        {
            builders[labelId] = new DetailBuilder();
        }

        foreach (var property in properties)
        {
            if (!TrySplitKey(property.Key, out var labelId, out var attribute) ||
                !builders.TryGetValue(labelId, out var builder))
            {
                continue;
            }

            builder.Apply(attribute, property.Value);
        }

        return labelIds.Select(labelId => builders[labelId].Build(labelId)).ToArray();
    }

    /// <summary>Splits <c>MSIP_Label_{guid}_{attribute}</c> into its GUID and attribute name.</summary>
    private static bool TrySplitKey(string key, out Guid labelId, out string attribute)
    {
        labelId = default;
        attribute = string.Empty;

        if (!key.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = key[Prefix.Length..];

        // GUID 자체가 하이픈을 포함하므로 마지막 밑줄에서 자른다.
        var separator = remainder.LastIndexOf('_');
        if (separator <= 0 || separator == remainder.Length - 1)
        {
            return false;
        }

        if (!Guid.TryParse(remainder[..separator], out labelId))
        {
            return false;
        }

        attribute = remainder[(separator + 1)..];
        return true;
    }

    private static bool IsActive(string value) =>
        value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);

    private sealed class DetailBuilder
    {
        private Guid? _siteId;
        private string? _name;
        private LabelMethod _method;
        private int? _contentBits;
        private Guid? _actionId;
        private DateTimeOffset? _setDate;

        internal void Apply(string attribute, string value)
        {
            switch (attribute)
            {
                case "SiteId":
                    if (Guid.TryParse(value, out var siteId))
                    {
                        _siteId = siteId;
                    }

                    break;

                case "Name":
                    _name = value.Length > 0 ? value : null;
                    break;

                case "Method":
                    _method = ParseMethod(value);
                    break;

                case "ContentBits":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits))
                    {
                        _contentBits = bits;
                    }

                    break;

                case "ActionId":
                    if (Guid.TryParse(value, out var actionId))
                    {
                        _actionId = actionId;
                    }

                    break;

                case "SetDate":
                    if (DateTimeOffset.TryParse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind,
                            out var setDate))
                    {
                        _setDate = setDate;
                    }

                    break;
            }
        }

        internal LabelDetail Build(Guid labelId) => new()
        {
            Id = labelId,
            SiteId = _siteId,
            Name = _name,
            Method = _method,
            ContentBits = _contentBits,
            ActionId = _actionId,
            SetDate = _setDate
        };

        private static LabelMethod ParseMethod(string value) => value switch
        {
            _ when value.Equals("Privileged", StringComparison.OrdinalIgnoreCase) =>
                LabelMethod.Privileged,
            _ when value.Equals("Standard", StringComparison.OrdinalIgnoreCase) =>
                LabelMethod.Standard,
            _ => LabelMethod.Unspecified
        };
    }
}
