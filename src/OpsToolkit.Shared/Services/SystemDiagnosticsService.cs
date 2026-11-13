using System.Globalization;
using System.Runtime.InteropServices;
using OpsToolkit.Shared.Models;

namespace OpsToolkit.Shared.Services;

public sealed class SystemDiagnosticsService
{
    public string GenerateTextReport()
    {
        var info = GetDiagnostics();
        return string.Create(CultureInfo.InvariantCulture, $"""
            === System diagnostics report ===
            Timestamp: {info.Timestamp:O}
            Machine: {info.MachineName}
            Operating system: {info.OsVersion} ({info.OsArchitecture})
            .NET runtime: {info.DotNetRuntimeVersion}
            Processor: {info.ProcessorName} ({info.ProcessorCount} logical processors)
            Runtime available memory budget (bytes): {info.AvailableMemoryBytes}
            Current process working set (bytes): {info.CurrentProcessMemoryBytes}
            =================================
            """);
    }

    public SystemDiagnosticsInfo GetDiagnostics() => new()
    {
        OsVersion = RuntimeInformation.OSDescription,
        OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
        ProcessorName = GetProcessorName(),
        ProcessorCount = Environment.ProcessorCount,
        AvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
        DotNetRuntimeVersion = RuntimeInformation.FrameworkDescription,
        CurrentProcessMemoryBytes = Environment.WorkingSet,
        MachineName = Environment.MachineName,
        Timestamp = DateTimeOffset.UtcNow
    };

    private static string GetProcessorName()
    {
        var identifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        return string.IsNullOrWhiteSpace(identifier) ? "Unknown Processor" : identifier.Trim();
    }
}
