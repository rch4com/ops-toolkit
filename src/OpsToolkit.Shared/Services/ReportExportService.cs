using System.Globalization;
using System.Text;
using System.Text.Json;
using OpsToolkit.Shared.Models;

namespace OpsToolkit.Shared.Services;

public sealed class ReportExportService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string ExportToCsv(IEnumerable<ReportItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var output = new StringBuilder("Id,FileName,FilePath,FileSizeBytes,Status,ProgressPercent,ErrorMessage,ResultDetails,ProcessedAt\r\n");
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            output.AppendJoin(',', new[]
            {
                Escape(item.Id), Escape(item.FileName), Escape(item.FilePath),
                item.FileSizeBytes.ToString(CultureInfo.InvariantCulture), Escape(item.Status),
                item.ProgressPercent.ToString("F1", CultureInfo.InvariantCulture), Escape(item.ErrorMessage),
                Escape(item.ResultDetails), Escape(item.ProcessedAt?.ToString("O", CultureInfo.InvariantCulture))
            });
            output.Append("\r\n");
        }
        return output.ToString();
    }

    public string ExportToJson(IEnumerable<ReportItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return JsonSerializer.Serialize(items, Options);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) { return string.Empty; }
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') || value[0] is '\t' or '\r' or '\n')
        {
            value = "'" + value;
        }
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}
