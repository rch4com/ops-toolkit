using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.Cli;

public sealed class CliFileScanItem
{
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsDryRun { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Protection { get; set; } = string.Empty;
    public string? ProtectionDetail { get; set; }
    public IReadOnlyList<Guid> LabelIds { get; set; } = [];
    public IReadOnlyList<LabelSource> Sources { get; set; } = [];
    public IReadOnlyList<LabelDetail> Details { get; set; } = [];
    public double ElapsedMilliseconds { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class CliScanReport
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string TargetPath { get; set; } = string.Empty;
    public bool IsDryRun { get; set; }
    public int TotalFiles { get; set; }
    public int LabeledCount { get; set; }
    public int UnlabeledCount { get; set; }
    public int ProtectedUnknownCount { get; set; }
    public int UnsupportedOrInvalidCount { get; set; }
    public int ErrorCount { get; set; }
    public List<CliFileScanItem> Items { get; set; } = [];
}

public static class CliRunner
{
    public const int ExitSuccess = 0;
    public const int ExitFailure = 1;
    public const int ExitUsageError = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(
        CliOptions options,
        MipLabelChecker checker,
        TextWriter stdOut,
        TextWriter stdErr,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(stdOut);
        ArgumentNullException.ThrowIfNull(stdErr);

        if (options.ShowHelp)
        {
            WriteHelp(stdOut);
            return ExitSuccess;
        }

        if (options.ShowVersion)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            await stdOut.WriteLineAsync($"MIPLabelCustomCheck.Explorer v{version}").ConfigureAwait(false);
            return ExitSuccess;
        }

        if (string.IsNullOrWhiteSpace(options.TargetPath))
        {
            await stdErr.WriteLineAsync("[오류] 검사할 파일 또는 폴더 경로(--path <path>)를 지정해 주세요.").ConfigureAwait(false);
            return ExitUsageError;
        }

        string fullTarget = Path.GetFullPath(options.TargetPath);
        List<string> targetFiles = [];

        if (File.Exists(fullTarget))
        {
            targetFiles.Add(fullTarget);
        }
        else if (Directory.Exists(fullTarget))
        {
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = options.Recursive,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            };

            foreach (string file in Directory.EnumerateFiles(fullTarget, "*", enumOptions))
            {
                if (options.AllFiles || LabelableExtensions.Includes(LabelableExtensions.Default, file) || IsExtraSupportedExtension(file))
                {
                    targetFiles.Add(file);
                }
            }

            targetFiles.Sort(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            await stdErr.WriteLineAsync($"[오류] 지정한 경로가 존재하지 않습니다: '{fullTarget}'").ConfigureAwait(false);
            return ExitFailure;
        }

        var report = new CliScanReport
        {
            TargetPath = fullTarget,
            IsDryRun = options.IsDryRun,
            TotalFiles = targetFiles.Count
        };

        LabelCheckOptions checkOptions = options.WithDetails ? LabelCheckOptions.WithDetails : LabelCheckOptions.Default;

        foreach (string filePath in targetFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long fileSize = 0;
            try
            {
                fileSize = new FileInfo(filePath).Length;
            }
            catch (IOException)
            {
                // Ignore size lookup failure
            }

            if (options.IsDryRun)
            {
                report.Items.Add(new CliFileScanItem
                {
                    FilePath = filePath,
                    FileSizeBytes = fileSize,
                    IsDryRun = true,
                    Status = "DryRunCandidate",
                    Protection = "NotInspected"
                });
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                LabelCheckResult result = await checker.CheckAsync(filePath, checkOptions, cancellationToken).ConfigureAwait(false);
                sw.Stop();

                switch (result.Status)
                {
                    case LabelStatus.Labeled:
                        report.LabeledCount++;
                        break;
                    case LabelStatus.Unlabeled:
                        report.UnlabeledCount++;
                        break;
                    case LabelStatus.ProtectedLabelUnknown:
                        report.ProtectedUnknownCount++;
                        break;
                    default:
                        report.UnsupportedOrInvalidCount++;
                        break;
                }

                report.Items.Add(new CliFileScanItem
                {
                    FilePath = filePath,
                    FileSizeBytes = fileSize,
                    IsDryRun = false,
                    Status = result.Status.ToString(),
                    Protection = result.Protection.ToString(),
                    ProtectionDetail = string.IsNullOrEmpty(result.ProtectionDetail) ? null : result.ProtectionDetail,
                    LabelIds = result.LabelIds,
                    Sources = result.Sources,
                    Details = result.Details,
                    ElapsedMilliseconds = Math.Round(sw.Elapsed.TotalMilliseconds, 3)
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                report.ErrorCount++;
                report.Items.Add(new CliFileScanItem
                {
                    FilePath = filePath,
                    FileSizeBytes = fileSize,
                    IsDryRun = false,
                    Status = "Error",
                    Protection = "Unknown",
                    ElapsedMilliseconds = Math.Round(sw.Elapsed.TotalMilliseconds, 3),
                    ErrorMessage = ex.Message
                });
            }
        }

        if (options.OutputJson)
        {
            await stdOut.WriteLineAsync(JsonSerializer.Serialize(report, JsonOptions)).ConfigureAwait(false);
        }
        else
        {
            WriteConsoleReport(stdOut, report, options);
        }

        return report.ErrorCount == 0 ? ExitSuccess : ExitFailure;
    }

    private static bool IsExtraSupportedExtension(string filePath)
    {
        string ext = Path.GetExtension(filePath);
        return string.Equals(ext, ".msg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ext, ".eml", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteConsoleReport(TextWriter writer, CliScanReport report, CliOptions options)
    {
        string modeText = report.IsDryRun ? "DRY-RUN (실제 파싱 없이 대상 파일 목록만 점검)" : "MIP 민감도 레이블 검사";
        writer.WriteLine($"[실행 모드] {modeText}");
        writer.WriteLine($"[대상 경로] {report.TargetPath} (총 {report.TotalFiles}개 파일)");
        writer.WriteLine(new string('-', 80));

        foreach (CliFileScanItem item in report.Items)
        {
            if (item.IsDryRun)
            {
                writer.WriteLine($"  [DRY-RUN] {item.FilePath} ({item.FileSizeBytes:N0} bytes)");
                continue;
            }

            if (item.ErrorMessage is not null)
            {
                writer.WriteLine($"  [ERROR] {item.FilePath} -> {item.ErrorMessage}");
                continue;
            }

            string labelSummary = item.LabelIds.Count > 0
                ? $"Labels: [{string.Join(", ", item.LabelIds)}]"
                : "Labels: -";
            string timeSuffix = options.Verbose ? $" ({item.ElapsedMilliseconds:F2} ms)" : string.Empty;

            writer.WriteLine($"  [{item.Status}] {Path.GetFileName(item.FilePath)} | Protection: {item.Protection} | {labelSummary}{timeSuffix}");

            if (options.WithDetails && item.Details.Count > 0)
            {
                foreach (LabelDetail detail in item.Details)
                {
                    writer.WriteLine($"      ↳ Label {detail.Id}: Name='{detail.Name ?? "-"}', SiteId={detail.SiteId?.ToString() ?? "-"}, Method={detail.Method}, SetDate={detail.SetDate?.ToString("o") ?? "-"}");
                }
            }
        }

        writer.WriteLine(new string('-', 80));
        if (report.IsDryRun)
        {
            writer.WriteLine($"[Dry-Run 완료] 검사 예정 파일: {report.TotalFiles}개");
        }
        else
        {
            writer.WriteLine($"[요약] 전체: {report.TotalFiles} | Labeled: {report.LabeledCount} | Unlabeled: {report.UnlabeledCount} | Protected(Unknown): {report.ProtectedUnknownCount} | 기타: {report.UnsupportedOrInvalidCount} | 오류: {report.ErrorCount}");
        }
    }

    public static void WriteHelp(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("MIPLabelCustomCheck.Explorer — 무의존성 Microsoft Purview(MIP) 민감도 레이블 및 보호 상태 검사 도구");
        writer.WriteLine();
        writer.WriteLine("사용법:");
        writer.WriteLine("  MIPLabelCustomCheck.Explorer                                인자 없이 실행 시 WPF 탐색기 GUI 실행");
        writer.WriteLine("  MIPLabelCustomCheck.Explorer --gui                          WPF 탐색기 GUI 명시적 실행");
        writer.WriteLine("  MIPLabelCustomCheck.Explorer --path <file|dir> [옵션]       CLI 모드로 파일 또는 폴더 스캔");
        writer.WriteLine();
        writer.WriteLine("주요 옵션:");
        writer.WriteLine("  -p, --path <path>      검사할 단일 파일 또는 디렉터리 경로");
        writer.WriteLine("  -r, --recursive        디렉터리 지정 시 하위 폴더까지 재귀 탐색");
        writer.WriteLine("  -a, --all-files        알려진 레이블 지원 확장자 외의 모든 파일 포함");
        writer.WriteLine("  -d, --details          레이블 상세 메타데이터(SiteId, Name, Method, SetDate 등) 파싱");
        writer.WriteLine("      --dry-run          실제 파일 파싱 없이 검사 대상 파일 목록과 크기만 사전 점검");
        writer.WriteLine("      --json             결과를 JSON 포맷으로 출력");
        writer.WriteLine("  -v, --verbose          파일별 검사 소요 시간(ms) 등 상세 정보 표시");
        writer.WriteLine("  -h, --help             도움말 출력");
        writer.WriteLine("      --version          버전 출력");
    }
}
