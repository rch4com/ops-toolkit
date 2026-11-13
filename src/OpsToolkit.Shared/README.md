# OpsToolkit.Shared

UI-independent .NET 10 services extracted from Utilities winui-app after approval of Phase 2 (scaffolder excluded). BCL-only runtime.

Consumers reference OpsToolkit.Shared.csproj. Construct PresetService and RecentHistoryService with explicit JSON file paths; no application directory is selected. Await methods with a CancellationToken. SettingsJson is opaque: callers must exclude passwords and other secrets before saving.

LogTailService streams complete UTF-8 lines, detects append/truncation/replacement and supports initial line limits. ReportExportService produces CSV/JSON from plain ReportItem DTOs. SystemDiagnosticsService returns local runtime diagnostics, not network probes.

Build: dotnet build src/OpsToolkit.Shared/OpsToolkit.Shared.csproj -warnaserror
Test: dotnet test tests/dotnet/OpsToolkit.Shared.Tests/OpsToolkit.Shared.Tests.csproj --nologo

This is a library, so CLI options/exit codes and dry-run belong to callers. Storage updates replace JSON atomically; malformed JSON is surfaced and preserved. Concurrent processes writing one store are unsupported.

Tail reads UTF-8 only. Prefix/tail fingerprints may miss middle-only edits. I/O errors, including temporary disappearance during rotation, propagate for caller retry. A partial line or one polling batch may grow with incoming data. Share one service instance per store; separate instances/processes have no transaction locking. Dispose preset/history services after use.

SystemDiagnosticsService.GenerateTextReport() provides a readable report with processor identity from PROCESSOR_IDENTIFIER (or Unknown Processor), OS/runtime, logical processor count, runtime memory budget and current process working set.
