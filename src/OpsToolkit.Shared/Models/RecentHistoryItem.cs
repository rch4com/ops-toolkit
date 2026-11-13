namespace OpsToolkit.Shared.Models;

public class RecentHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string ActionType { get; set; } = "Process";
    public bool IsSuccess { get; set; } = true;
    public string? Note { get; set; }
}
