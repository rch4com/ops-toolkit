namespace OpsToolkit.Shared.Models;

public sealed class SystemDiagnosticsInfo
{
    public string OsVersion { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string ProcessorName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public string DotNetRuntimeVersion { get; set; } = string.Empty;
    public long CurrentProcessMemoryBytes { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
