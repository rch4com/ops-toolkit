namespace OpsToolkit.Shared.Models;

public sealed class ReportItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public double ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultDetails { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
