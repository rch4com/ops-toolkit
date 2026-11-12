using System.IO;
using MIPLabelCustomCheck.Explorer.Scanning;

namespace MIPLabelCustomCheck.Explorer.ViewModels;

/// <summary>One file's row in the list and its detail-pane content.</summary>
public sealed class FileEntryViewModel : ObservableObject
{
    private CheckState _state = CheckState.Pending;
    private LabelCheckResult? _result;
    private Exception? _error;
    private TimeSpan? _elapsed;
    private TimeSpan? _remeasured;

    public FileEntryViewModel(string filePath, long sizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        Name = Path.GetFileName(filePath);
        SizeBytes = sizeBytes;
    }

    public string FilePath { get; }

    public string Name { get; }

    public long SizeBytes { get; }

    public CheckState State
    {
        get => _state;
        private set
        {
            if (Set(ref _state, value))
            {
                Raise(nameof(StatusText));
            }
        }
    }

    public LabelCheckResult? Result
    {
        get => _result;
        private set
        {
            if (Set(ref _result, value))
            {
                Raise(nameof(StatusText));
                Raise(nameof(LabelText));
                Raise(nameof(ProtectionText));
                Raise(nameof(SourcesText));
                Raise(nameof(Details));
                Raise(nameof(HasDetails));
            }
        }
    }

    public Exception? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                Raise(nameof(StatusText));
            }
        }
    }

    /// <summary>Time the scan measured for this file, under parallel load.</summary>
    public TimeSpan? Elapsed
    {
        get => _elapsed;
        private set => Set(ref _elapsed, value);
    }

    /// <summary>Time a standalone re-measurement took, kept separate from <see cref="Elapsed"/>.</summary>
    public TimeSpan? Remeasured
    {
        get => _remeasured;
        private set => Set(ref _remeasured, value);
    }

    public string StatusText => _state switch
    {
        CheckState.Pending => "대기",
        CheckState.Running => "검사중",
        CheckState.Skipped => "대상 외",
        CheckState.Canceled => "취소됨",
        CheckState.Failed => _error is MipLabelCheckException { Reason: MipLabelCheckErrorReason.FileLocked }
            ? "잠김"
            : "오류",
        CheckState.Completed => _result?.Status switch
        {
            LabelStatus.Labeled => "레이블됨",
            LabelStatus.Unlabeled => "미레이블",
            LabelStatus.ProtectedLabelUnknown => "보호됨 (레이블 불명)",
            LabelStatus.Unsupported => "미지원",
            LabelStatus.InvalidFile => "잘못된 파일",
            _ => "알 수 없음"
        },
        _ => "알 수 없음"
    };

    public string LabelText => _result is { Status: LabelStatus.Labeled }
        ? $"{_result.LabelIds.Count}개"
        : "-";

    /// <summary>Each label's supplementary metadata, ready to bind. Empty unless it was asked for.</summary>
    public IReadOnlyList<LabelDetailViewModel> Details =>
        _result?.Details.Select(detail => new LabelDetailViewModel(detail)).ToArray() ?? [];

    /// <summary>
    /// Whether there is supplementary metadata to show. False both when details were not
    /// requested and when the file carries no label, so the detail pane can hide the section
    /// rather than showing a row of dashes.
    /// </summary>
    public bool HasDetails => _result is { Details.Count: > 0 };

    /// <summary>
    /// The detected protection, in the same language as the rest of the UI. Binding straight to
    /// <see cref="LabelCheckResult.Protection"/> would print the raw enum name instead.
    /// </summary>
    public string ProtectionText => _result?.Protection switch
    {
        ProtectionType.None => "없음",
        ProtectionType.Native => "네이티브 (형식 자체 암호화)",
        ProtectionType.GenericPFile => "일반 .pfile",
        _ => "-"
    };

    /// <summary>
    /// Where each reported label GUID was first seen, in the same language as the rest of the
    /// UI. Binding straight to <see cref="LabelCheckResult.Sources"/> would print raw enum
    /// names instead. "-" when the file carries no reported label.
    /// </summary>
    public string SourcesText => _result is { Status: LabelStatus.Labeled, Sources.Count: > 0 }
        ? string.Join(", ", _result.Sources.Distinct().Select(FormatSource))
        : "-";

    private static string FormatSource(LabelSource source) => source switch
    {
        LabelSource.LabelInfo => "LabelInfo",
        LabelSource.LegacyProperty => "레거시 속성",
        LabelSource.Xmp => "XMP",
        LabelSource.PdfInfoDictionary => "PDF Info 사전",
        _ => source.ToString()
    };

    public void MarkRunning()
    {
        State = CheckState.Running;
        Error = null;
    }

    /// <summary>Marks the file as outside the configured extension set, so the scan skipped it.</summary>
    public void MarkSkipped()
    {
        Result = null;
        Error = null;
        Elapsed = null;
        State = CheckState.Skipped;
    }

    /// <summary>Applies one scan report. A null <paramref name="result"/> means the file was cancelled.</summary>
    public void Apply(TimedCheckResult? result)
    {
        if (result is null)
        {
            Result = null;
            Error = null;
            Elapsed = null;
            State = CheckState.Canceled;
            return;
        }

        Result = result.Result;
        Error = result.Error;
        Elapsed = result.Elapsed;
        State = result.IsSuccess ? CheckState.Completed : CheckState.Failed;
    }

    /// <summary>Records a standalone re-measurement without disturbing the scan's own timing.</summary>
    public void ApplyRemeasurement(TimedCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result.Result;
        Error = result.Error;
        Remeasured = result.Elapsed;
        State = result.IsSuccess ? CheckState.Completed : CheckState.Failed;
    }
}
