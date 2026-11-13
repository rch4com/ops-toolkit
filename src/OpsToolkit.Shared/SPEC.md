# Specification

Stored record envelopes reject unknown JSON members. This preserves forward/foreign fields by failing reads and mutations instead of silently dropping data. Opaque SettingsJson remains the caller's typed validation responsibility.

Services use OpsToolkit.Shared.Services, DTOs OpsToolkit.Shared.Models. PresetService(string filePath): GetPresetsAsync, SavePresetAsync(PresetProfile), DeletePresetAsync(string id), all with optional CancellationToken. Profile retains Id, Name, Description, SettingsJson, IsDefault, CreatedAt, UpdatedAt and parameterless/string constructors. Save upserts id and enforces a single default. SettingsJson must be valid JSON but remains opaque.

RecentHistoryService(string filePath): GetHistoryAsync, AddItemAsync(path, actionType="Process", isSuccess=true, note=null, cancellationToken=default), ClearHistoryAsync. Keep latest 10; deduplicate paths case-insensitively; preserve source RecentHistoryItem fields. Missing stores are empty. Invalid JSON (including null root) throws JsonException; no mutation may overwrite it. Saves use a unique same-directory temporary file then rename. Cancellation before replacement preserves previous data. Service instance operations serialize; no cross-process locking contract.

LogTailService.ReadLinesAsync(path, initialReadLines=100, pollingInterval=null, cancellationToken=default) returns an async sequence of newline-complete UTF-8 lines. Partial lines remain pending. A bounded byte fingerprint detects replacement and truncation, then rereads new contents. Cancellation interrupts reading/polling. No pause API: consumers pause enumeration.

ReportExportService.ExportToCsv/ExportToJson accept IEnumerable<ReportItem>. CSV quotes commas, quotes and newlines and neutralizes spreadsheet formula prefixes. JSON preserves DTO values. Diagnostics are local BCL runtime/OS/process data; GC available memory is explicitly not physical RAM.

Argument failures throw ArgumentException; I/O, JSON and cancellation exceptions propagate. Library has no CLI/exit codes; callers implement dry-run before mutations.

Tail fingerprinting samples 128-byte prefix/tail windows; middle-only edits can be missed. A missing file or I/O failure propagates, allowing caller retry. Per-line/poll-batch memory scales with incoming data. Preset/history services are IDisposable and should have a single owner per file.

Diagnostics retain ProcessorName (PROCESSOR_IDENTIFIER environment value, or Unknown Processor fallback) and GenerateTextReport(). Text output uses invariant numeric formatting and labels AvailableMemoryBytes as runtime available memory budget, not physical RAM.
