#Requires -Version 5.1
<#
.SYNOPSIS
    Cron-Safe 파일명 기반 PowerShell 작업 스케줄러 메인 엔진입니다.

.DESCRIPTION
    jobs 폴더에 있는 여러 .ps1 파일을 파일명 끝의 "Cron-Safe(분 시 일 월 요일) [옵션]" 규칙으로 자동 실행합니다.
    - Windows 파일명 제한으로 인해 * 대신 X, / 대신 @ 표기를 사용합니다.
    - 요일(DOW): SUN,MON,TUE,WED,THU,FRI,SAT 또는 0-7 (0/7=일요일)
    - 월말 옵션: 일(DOM) 필드에 L 또는 L-n 사용 가능
    - 부가 옵션: TIMEOUT=<초>, CB=<횟수>, NOW=1, LOG=1, CONCURRENT=1
    - 실패 시 단계별 재시도(Backoff), 서킷 브레이커, Windows 이벤트 로그, 일자별 UTF-8 로그 및 자동 복구를 지원합니다.

.PARAMETER BaseDir
    스케줄러 기본 디렉터리 경로입니다 (기본값: 스크립트 위치).

.PARAMETER TickSeconds
    메인 루프 점검 주기(초)입니다 (기본값: 5).

.PARAMETER MaxParallel
    동시 병렬 실행 가능한 최대 작업 수입니다 (기본값: 10).

.PARAMETER EventLogName
    기록할 Windows 이벤트 로그 이름입니다 (기본값: PSJobScheduler-Logs).

.PARAMETER EventSource
    기록할 Windows 이벤트 소스 이름입니다 (기본값: PSJobScheduler).

.PARAMETER RunOnce
    지정 시 메인 루프를 1회만 수행하고 종료합니다 (점검 및 테스트용).

.EXAMPLE
    .\schedule.ps1
    스케줄러를 상시 실행합니다. 종료하려면 같은 폴더에 schedule.stop 파일을 생성합니다.

.EXAMPLE
    .\schedule.ps1 -RunOnce -WhatIf
    하위 작업을 실제로 실행하지 않고 현재 시점에 트리거될 작업을 1회 시뮬레이션합니다.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(HelpMessage = '스케줄러 기본 디렉터리 경로')]
    [string]$BaseDir = $PSScriptRoot,

    [Parameter(HelpMessage = '스케줄러 루프 대기 시간(초)')]
    [int]$TickSeconds = 5,

    [Parameter(HelpMessage = '최대 병렬 실행 작업 수')]
    [int]$MaxParallel = 10,

    [Parameter(HelpMessage = 'Windows 이벤트 로그 이름')]
    [string]$EventLogName = 'PSJobScheduler-Logs',

    [Parameter(HelpMessage = 'Windows 이벤트 소스 이름')]
    [string]$EventSource = 'PSJobScheduler',

    [Parameter(HelpMessage = '1회 루프만 실행 후 종료')]
    [switch]$RunOnce
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ===================== 설정 =====================
if (-not $BaseDir) {
    $BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$JobsDir = Join-Path $BaseDir 'jobs'
$StateDir = Join-Path $BaseDir 'state'
$LogsRoot = Join-Path $BaseDir 'logs'
$StopFile = Join-Path $BaseDir 'schedule.stop'

# 캐시 변수
$Script:JobKeyCache = @{}
$Script:LogCheckedCache = @{} # 로그 파일 경로 => LastWriteTime.Ticks

# 로그 볼륨 제어
$LogOutputOnSuccess = $false   # true면 성공 시에도 출력 로그 저장
$WritePerRunJobLogs = $false   # true면 실행_<RunId>.log 파일을 개별 생성

# 재시도 정책 (ExitCode != 0 또는 시작 실패)
$RetryEnabled = $true
$RetryBackoffMin = @(1, 5, 15)
$MaxRetries = $RetryBackoffMin.Count

# 서킷브레이커 기본값(파일명에 CB= 로 개별 지정 가능)
$DefaultCircuitFailures = 5
$DefaultCircuitCoolDownMin = 60

# 타임아웃 기본값(파일명에 TIMEOUT= 로 개별 지정 가능)
$DefaultTimeoutSec = 0

# 롤링 보관기간
$LogRetentionDays = 14
$StateRetentionDays = 30

New-Item -ItemType Directory -Force -Path $JobsDir, $StateDir, $LogsRoot -WhatIf:$false -Confirm:$false | Out-Null

# ===================== 유틸: 로그 =====================
function Get-SchedulerLogPath {
    $date = Get-Date -Format 'yyyyMMdd'
    return (Join-Path $LogsRoot "스케줄러_$date.log")
}

function Set-Utf8LogFile {
    param(
        [Parameter(Mandatory = $true)] [string]$Path,
        [ValidateSet('Ansi', 'Oem')] [string]$AssumeEncoding = 'Ansi'
    )

    if (-not (Test-Path -LiteralPath $Path)) { return }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        if ($bytes.Length -eq 0) { return }

        # BOM 체크
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { return }
        if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
            $text = [System.Text.Encoding]::Unicode.GetString($bytes)
            [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($true)))
            return
        }
        if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
            $text = [System.Text.Encoding]::BigEndianUnicode.GetString($bytes)
            [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($true)))
            return
        }

        $utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
        try {
            [void]$utf8Strict.GetString($bytes)
            return
        }
        catch {
            # fall through
        }

        $srcEnc = if ($AssumeEncoding -eq 'Oem') {
            [System.Text.Encoding]::GetEncoding([System.Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage)
        }
        else {
            [System.Text.Encoding]::GetEncoding([System.Globalization.CultureInfo]::CurrentCulture.TextInfo.ANSICodePage)
        }

        $text = $srcEnc.GetString($bytes)
        [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($true)))
    }
    catch {
        return
    }
}

function Convert-TodaysLogsToUtf8 {
    $today = Get-Date -Format 'yyyyMMdd'

    $fileInfos = @()
    $schedulerLog = Get-SchedulerLogPath
    if (Test-Path -LiteralPath $schedulerLog) {
        try { $fileInfos += Get-Item -LiteralPath $schedulerLog } catch {}
    }

    $todayDirs = Get-ChildItem -Path $LogsRoot -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName $today } |
        Where-Object { Test-Path -LiteralPath $_ }

    foreach ($d in $todayDirs) {
        try {
            $fileInfos += Get-ChildItem -Path $d -Filter '*.log' -File -ErrorAction SilentlyContinue
        }
        catch {}
    }

    foreach ($fi in @($fileInfos)) {
        if ($null -eq $fi) { continue }
        $p = $fi.FullName
        $lastWrite = $fi.LastWriteTime.Ticks

        if ($Script:LogCheckedCache.ContainsKey($p) -and $Script:LogCheckedCache[$p] -eq $lastWrite) {
            continue
        }

        $assume = if ($fi.Name -like '출력_*') { 'Oem' } else { 'Ansi' }
        Set-Utf8LogFile -Path $p -AssumeEncoding $assume

        $Script:LogCheckedCache[$p] = $lastWrite
    }
}

function Write-SchedulerLog {
    param(
        [string]$Message,
        [ValidateSet('정보', '경고', '오류')] [string]$Level = '정보'
    )
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $line = "[$ts][$Level] $Message"
    $logPath = Get-SchedulerLogPath

    if (Test-Path -LiteralPath $logPath) {
        $fi = Get-Item -LiteralPath $logPath
        $lastWrite = $fi.LastWriteTime.Ticks
        if (-not $Script:LogCheckedCache.ContainsKey($logPath) -or $Script:LogCheckedCache[$logPath] -ne $lastWrite) {
            Set-Utf8LogFile -Path $logPath -AssumeEncoding 'Ansi'
            $Script:LogCheckedCache[$logPath] = $lastWrite
        }
    }

    Add-Content -Path $logPath -Value $line -Encoding UTF8 -WhatIf:$false -Confirm:$false
    Microsoft.PowerShell.Utility\Write-Host $line
}

function Get-JobLogDir {
    param([string]$JobBaseName)
    $d = Get-Date -Format 'yyyyMMdd'
    return (Join-Path (Join-Path $LogsRoot $JobBaseName) $d)
}

function Write-JobLog {
    param(
        [string]$JobBaseName,
        [string]$Message,
        [ValidateSet('정보', '경고', '오류')] [string]$Level = '정보',
        [string]$RunId = ''
    )
    $dir = Get-JobLogDir -JobBaseName $JobBaseName
    New-Item -ItemType Directory -Force -Path $dir -WhatIf:$false -Confirm:$false | Out-Null
    $ts = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $file = if ($RunId -and $WritePerRunJobLogs) { "실행_$RunId.log" } else { 'job.log' }

    $prefix = if ($RunId) { "JOB:${JobBaseName}:${RunId}" } else { "JOB:${JobBaseName}" }
    $line = "[$ts][$Level][$prefix] $Message"
    $logPath = Join-Path $dir $file

    if (Test-Path -LiteralPath $logPath) {
        $fi = Get-Item -LiteralPath $logPath
        $lastWrite = $fi.LastWriteTime.Ticks
        if (-not $Script:LogCheckedCache.ContainsKey($logPath) -or $Script:LogCheckedCache[$logPath] -ne $lastWrite) {
            Set-Utf8LogFile -Path $logPath -AssumeEncoding 'Ansi'
            $Script:LogCheckedCache[$logPath] = $lastWrite
        }
    }

    Add-Content -Path $logPath -Value $line -Encoding UTF8 -WhatIf:$false -Confirm:$false
    Microsoft.PowerShell.Utility\Write-Host $line
}

# ===================== 이벤트로그 =====================
function Initialize-EventSource {
    if ($WhatIfPreference) { return $false }
    try {
        if (-not [System.Diagnostics.EventLog]::SourceExists($EventSource)) {
            New-EventLog -LogName $EventLogName -Source $EventSource
        }
        return $true
    }
    catch {
        Write-SchedulerLog "이벤트 로그 Source 등록 실패(관리자 권한 필요할 수 있음): $($_.Exception.Message)" '경고'
        return $false
    }
}

$EventSourceReady = Initialize-EventSource

function Write-Event {
    param(
        [string]$Message,
        [ValidateSet('Information', 'Warning', 'Error')] [string]$EntryType = 'Information',
        [int]$EventId = 3000
    )
    if (-not $EventSourceReady -or $WhatIfPreference) { return }
    try {
        Write-EventLog -LogName $EventLogName -Source $EventSource -EntryType $EntryType -EventId $EventId -Message $Message
    }
    catch {
        Write-SchedulerLog "Write-EventLog 실패: $($_.Exception.Message)" '경고'
    }
}

# ===================== 롤링 삭제 =====================
function Remove-OldItems {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([string]$Path, [int]$RetentionDays)
    if (-not (Test-Path $Path)) { return }
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -Path $Path -Recurse -Force -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        ForEach-Object {
            if ($PSCmdlet.ShouldProcess($_.FullName, '오래된 상태 파일 정리')) {
                try { Remove-Item -LiteralPath $_.FullName -Force -Recurse -Confirm:$false -ErrorAction SilentlyContinue } catch {}
            }
        }
}

function Remove-OldLogs {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([int]$RetentionDays)

    if (-not (Test-Path -LiteralPath $LogsRoot)) { return }
    $cutoff = (Get-Date).AddDays(-$RetentionDays)

    try {
        Get-ChildItem -Path $LogsRoot -Filter '스케줄러_*.log' -File -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt $cutoff } |
            ForEach-Object {
                if ($PSCmdlet.ShouldProcess($_.FullName, '오래된 스케줄러 로그 정리')) {
                    try { Remove-Item -LiteralPath $_.FullName -Force -Confirm:$false -ErrorAction SilentlyContinue } catch {}
                }
            }
    }
    catch {}

    try {
        $jobDirs = Get-ChildItem -Path $LogsRoot -Directory -Force -ErrorAction SilentlyContinue
        foreach ($jd in $jobDirs) {
            try {
                Get-ChildItem -Path $jd.FullName -Directory -Force -ErrorAction SilentlyContinue |
                    Where-Object {
                        $_.Name -match '^\d{8}$' -and ([datetime]::ParseExact($_.Name, 'yyyyMMdd', $null)) -lt $cutoff.Date
                    } |
                    ForEach-Object {
                        if ($PSCmdlet.ShouldProcess($_.FullName, '오래된 작업 로그 폴더 정리')) {
                            try { Remove-Item -LiteralPath $_.FullName -Force -Recurse -Confirm:$false -ErrorAction SilentlyContinue } catch {}
                        }
                    }
            }
            catch {}
        }
    }
    catch {}
}

function Test-ProcessAlive {
    param([int]$ProcessId)

    if ($ProcessId -le 0) { return $false }

    try {
        $proc = Get-Process -Id $ProcessId -ErrorAction Stop
        return (-not $proc.HasExited)
    }
    catch {
        return $false
    }
}

function Clear-PendingStateFields {
    param([string]$StatePath)

    if (-not (Test-Path -LiteralPath $StatePath)) { return }

    try {
        $state = Get-Content -Raw -Path $StatePath | ConvertFrom-Json
        if ($state.PSObject.Properties.Name -contains 'PendingPid') { $state.PendingPid = $null }
        if ($state.PSObject.Properties.Name -contains 'PendingStartTime') { $state.PendingStartTime = $null }
        if ($state.PSObject.Properties.Name -contains 'PendingRunId') { $state.PendingRunId = $null }
        ($state | ConvertTo-Json -Depth 10) | Set-Content -Path $StatePath -Encoding UTF8 -WhatIf:$false -Confirm:$false
    }
    catch {}
}

function Repair-StaleLock {
    param([Parameter(Mandatory = $true)] [System.IO.FileInfo]$LockFile)

    if ($LockFile.Name -notmatch '^job_([0-9a-f]+)\.lock$') {
        return $false
    }

    $jobKey = $Matches[1]
    $statePath = Join-Path $StateDir ("job_" + $jobKey + ".json")
    $pendingPid = 0
    $pendingStartTime = $null
    $pendingRunId = $null

    if (Test-Path -LiteralPath $statePath) {
        try {
            $state = Get-Content -Raw -Path $statePath | ConvertFrom-Json
            if ($state.PSObject.Properties.Name -contains 'PendingPid' -and $state.PendingPid) {
                $pendingPid = [int]$state.PendingPid
            }
            if ($state.PSObject.Properties.Name -contains 'PendingStartTime' -and $state.PendingStartTime) {
                $pendingStartTime = $state.PendingStartTime
            }
            if ($state.PSObject.Properties.Name -contains 'PendingRunId' -and $state.PendingRunId) {
                $pendingRunId = [string]$state.PendingRunId
            }
        }
        catch {}
    }

    if ($pendingRunId) {
        $completionPath = Join-Path $StateDir ("complete_" + $jobKey + "_" + $pendingRunId + ".json")
        if (Test-Path -LiteralPath $completionPath) {
            try { Remove-Item -LiteralPath $LockFile.FullName -Force -Confirm:$false -ErrorAction SilentlyContinue } catch {}
            return $false
        }
    }

    if ($pendingPid -gt 0) {
        try {
            $proc = Get-Process -Id $pendingPid -ErrorAction Stop
            if (-not $proc.HasExited) {
                if ($pendingStartTime) {
                    $actualStart = $proc.StartTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
                    if ($actualStart -eq $pendingStartTime) {
                        return $true
                    }
                }
                else {
                    return $true
                }
            }
        }
        catch {}
    }

    try { Remove-Item -LiteralPath $LockFile.FullName -Force -Confirm:$false -ErrorAction SilentlyContinue } catch {}
    Clear-PendingStateFields -StatePath $statePath
    Write-SchedulerLog "stale lock removed: $($LockFile.Name) (PID reuse or terminated)" '경고'
    return $false
}

function Get-ActiveLockCount {
    $active = 0
    $locks = Get-ChildItem -Path $StateDir -Filter '*.lock' -File -ErrorAction SilentlyContinue
    foreach ($lock in $locks) {
        if (Repair-StaleLock -LockFile $lock) {
            $active++
        }
    }
    return $active
}

# ===================== 상태/키 =====================
function Get-JobKey {
    param([string]$JobFullPath)
    $normalizedPath = $JobFullPath.ToLowerInvariant()

    if ($Script:JobKeyCache.ContainsKey($normalizedPath)) {
        return $Script:JobKeyCache[$normalizedPath]
    }

    $bytes = [Text.Encoding]::UTF8.GetBytes($normalizedPath)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
        $key = ([BitConverter]::ToString($hash) -replace '-', '').ToLower()
        $Script:JobKeyCache[$normalizedPath] = $key
        return $key
    }
    finally { $sha.Dispose() }
}

function Get-LockPath { param([string]$JobFullPath) (Join-Path $StateDir ("job_" + (Get-JobKey $JobFullPath) + ".lock")) }
function Get-JobStatePath { param([string]$JobFullPath) (Join-Path $StateDir ("job_" + (Get-JobKey $JobFullPath) + ".json")) }
function Get-CompletionPath { param([string]$JobFullPath, [string]$RunId) (Join-Path $StateDir ("complete_" + (Get-JobKey $JobFullPath) + "_" + $RunId + ".json")) }

function Get-JobState {
    param([string]$JobFullPath)
    $p = Get-JobStatePath $JobFullPath
    if (-not (Test-Path $p)) {
        return [pscustomobject]@{
            NextRun             = $null
            RetryCount          = 0
            NextRetry           = $null
            LastExitCode        = $null
            LastRun             = $null
            PendingPid          = $null
            PendingStartTime    = $null
            PendingRunId        = $null
            ConsecutiveFailures = 0
            CircuitOpenUntil    = $null
            NowConsumed         = $false
        }
    }

    function ConvertTo-LocalDateTimeOrNull {
        param($Value)
        if ($null -eq $Value) { return $null }

        if ($Value -is [datetime]) {
            if ($Value.Kind -eq [System.DateTimeKind]::Utc) { return $Value.ToLocalTime() }
            return $Value
        }

        try {
            $dt = [datetime]$Value
            if ($dt.Kind -eq [System.DateTimeKind]::Utc) { return $dt.ToLocalTime() }
            return $dt
        }
        catch {
            return $null
        }
    }

    try {
        $obj = (Get-Content -Raw -Path $p | ConvertFrom-Json)

        if ($obj.PSObject.Properties.Name -contains 'NextRun') { $obj.NextRun = ConvertTo-LocalDateTimeOrNull $obj.NextRun }
        if ($obj.PSObject.Properties.Name -contains 'NextRetry') { $obj.NextRetry = ConvertTo-LocalDateTimeOrNull $obj.NextRetry }
        if ($obj.PSObject.Properties.Name -contains 'LastRun') { $obj.LastRun = ConvertTo-LocalDateTimeOrNull $obj.LastRun }
        if ($obj.PSObject.Properties.Name -contains 'CircuitOpenUntil') { $obj.CircuitOpenUntil = ConvertTo-LocalDateTimeOrNull $obj.CircuitOpenUntil }

        return $obj
    }
    catch {
        return [pscustomobject]@{
            NextRun             = $null
            RetryCount          = 0
            NextRetry           = $null
            LastExitCode        = $null
            LastRun             = $null
            PendingPid          = $null
            PendingStartTime    = $null
            PendingRunId        = $null
            ConsecutiveFailures = 0
            CircuitOpenUntil    = $null
            NowConsumed         = $false
        }
    }
}

function Save-JobState {
    param([string]$JobFullPath, $StateObj)
    if ($WhatIfPreference) { return }
    ($StateObj | ConvertTo-Json -Depth 10) | Set-Content -Path (Get-JobStatePath $JobFullPath) -Encoding UTF8 -WhatIf:$false -Confirm:$false
}

# ===================== cron 파싱 + 옵션 파싱 =====================
function ConvertFrom-JobFileName {
    param([string]$BaseNameNoExt)

    $tokens = @($BaseNameNoExt -split '\s+' | Where-Object { $_ -ne '' })
    if ($tokens.Count -lt 6) { return $null }

    $timeout = $DefaultTimeoutSec
    $cbN = $DefaultCircuitFailures
    $nowOpt = $false
    $logOpt = $false
    $concurrent = $false

    $i = $tokens.Count - 1
    while ($i -ge 0 -and ($tokens[$i] -match '^(TIMEOUT|CB|NOW|LOG|CONCURRENT)=(.+)$')) {
        $key = $Matches[1].ToUpper()
        $val = $Matches[2].ToUpper()

        switch ($key) {
            'TIMEOUT'    { $timeout = [int]$val }
            'CB'         { $cbN = [int]$val }
            'NOW'        { $nowOpt = ($val -eq '1' -or $val -eq 'TRUE') }
            'LOG'        { $logOpt = ($val -eq '1' -or $val -eq 'TRUE') }
            'CONCURRENT' { $concurrent = ($val -eq '1' -or $val -eq 'TRUE') }
        }
        $i--
    }

    if ($i -lt 4) { return $null }

    $dow = $tokens[$i--].ToUpper()
    $mon = $tokens[$i--].ToUpper()
    $dom = $tokens[$i--].ToUpper()
    $hour = $tokens[$i--].ToUpper()
    $min = $tokens[$i--].ToUpper()

    $prefix = if ($i -ge 0) { ($tokens[0..$i] -join ' ').Trim() } else { '' }

    return [pscustomobject]@{
        Prefix     = $prefix
        Cron       = [pscustomobject]@{ Min = $min; Hour = $hour; Dom = $dom; Mon = $mon; Dow = $dow }
        TimeoutSec = $timeout
        CircuitN   = $cbN
        NowOpt     = $nowOpt
        LogOpt     = $logOpt
        Concurrent = $concurrent
    }
}

function ConvertTo-NormalizedDowToken {
    param([string]$DowToken)
    $t = $DowToken.ToUpper()
    $map = @{ 'SUN' = '0'; 'MON' = '1'; 'TUE' = '2'; 'WED' = '3'; 'THU' = '4'; 'FRI' = '5'; 'SAT' = '6' }
    foreach ($k in $map.Keys) {
        $t = [regex]::Replace($t, "(?<![A-Z0-9])$k(?![A-Z0-9])", $map[$k])
    }
    return $t
}

function Expand-RangeList {
    param(
        [string]$Token,
        [int]$MinValue,
        [int]$MaxValue,
        [datetime]$ContextDate = (Get-Date),
        [switch]$AllowLastDay
    )

    $set = New-Object 'System.Collections.Generic.HashSet[int]'
    $parts = $Token.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }

    foreach ($pOriginal in $parts) {
        $p = $pOriginal.ToUpper()

        if ($AllowLastDay) {
            if ($p -eq 'L') {
                $last = [DateTime]::DaysInMonth($ContextDate.Year, $ContextDate.Month)
                [void]$set.Add($last); continue
            }
            if ($p -match '^L-(\d+)$') {
                $n = [int]$Matches[1]
                $last = [DateTime]::DaysInMonth($ContextDate.Year, $ContextDate.Month) - $n
                if ($last -ge $MinValue -and $last -le $MaxValue) { [void]$set.Add($last) }
                continue
            }
        }

        if ($p -eq 'X') {
            for ($i = $MinValue; $i -le $MaxValue; $i++) { [void]$set.Add($i) }
            continue
        }

        $step = 1
        $base = $p
        if ($p -match '^(.*)@(\d+)$') {
            $base = $Matches[1]
            $step = [int]$Matches[2]
            if ($step -le 0) { continue }
        }

        $start = $null; $end = $null
        if ($base -eq 'X' -or $base -eq '') {
            $start = $MinValue; $end = $MaxValue
        }
        elseif ($base -match '^(\d+)-(\d+)$') {
            $start = [int]$Matches[1]; $end = [int]$Matches[2]
        }
        elseif ($base -match '^(\d+)$') {
            $start = [int]$Matches[1]
            $end = if ($p -match '@') { $MaxValue } else { $start }
        }
        else { continue }

        if ($start -lt $MinValue) { $start = $MinValue }
        if ($end -gt $MaxValue) { $end = $MaxValue }
        if ($end -lt $start) { continue }

        for ($i = $start; $i -le $end; $i += $step) { [void]$set.Add($i) }
    }

    return ,$set
}

function New-CronMatcher {
    param($Cron, [datetime]$ContextDate)

    $mins = Expand-RangeList -Token $Cron.Min  -MinValue 0  -MaxValue 59  -ContextDate $ContextDate
    $hours = Expand-RangeList -Token $Cron.Hour -MinValue 0  -MaxValue 23  -ContextDate $ContextDate
    $mons = Expand-RangeList -Token $Cron.Mon  -MinValue 1  -MaxValue 12  -ContextDate $ContextDate
    $doms = Expand-RangeList -Token $Cron.Dom  -MinValue 1  -MaxValue 31  -ContextDate $ContextDate -AllowLastDay

    $dowToken = ConvertTo-NormalizedDowToken -DowToken $Cron.Dow
    $dowRaw = Expand-RangeList -Token $dowToken -MinValue 0 -MaxValue 7 -ContextDate $ContextDate
    $dows = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($d in $dowRaw) {
        $norm = if ($d -eq 7) { 0 } else { $d }
        [void]$dows.Add($norm)
    }

    $isDomWildcard = ($Cron.Dom.Trim().ToUpperInvariant() -eq 'X')
    $isDowWildcard = ($Cron.Dow.Trim().ToUpperInvariant() -eq 'X')

    return [pscustomobject]@{
        Minutes       = $mins
        Hours         = $hours
        Dom           = $doms
        Months        = $mons
        Dow           = $dows
        DomIsWildcard = $isDomWildcard
        DowIsWildcard = $isDowWildcard
    }
}

function Test-CronMatch {
    param([datetime]$Dt, $Matcher)

    function Test-SetContains {
        param($Set, [int]$Value)
        if ($null -eq $Set) { return $false }

        if ($Set -is [System.Collections.ICollection] -or $Set.PSObject.Methods.Name -contains 'Contains') {
            try { return [bool]$Set.Contains($Value) } catch { }
        }

        foreach ($v in $Set) { if ([int]$v -eq $Value) { return $true } }
        return ([int]$Set -eq $Value)
    }

    $baseMatch = (Test-SetContains -Set $Matcher.Minutes -Value $Dt.Minute) -and
        (Test-SetContains -Set $Matcher.Hours -Value $Dt.Hour) -and
        (Test-SetContains -Set $Matcher.Months -Value $Dt.Month)

    if (-not $baseMatch) { return $false }

    $domMatch = Test-SetContains -Set $Matcher.Dom -Value $Dt.Day
    $dowMatch = Test-SetContains -Set $Matcher.Dow -Value ([int]$Dt.DayOfWeek)

    if ($Matcher.DomIsWildcard -and $Matcher.DowIsWildcard) { return $true }
    if ($Matcher.DomIsWildcard) { return $dowMatch }
    if ($Matcher.DowIsWildcard) { return $domMatch }

    return ($domMatch -or $dowMatch)
}

function Get-PrevCronTime {
    param([datetime]$Now, $Cron)
    $t = (Get-Date $Now).AddSeconds(-$Now.Second).AddMilliseconds(-$Now.Millisecond)
    $curMonth = -1
    $matcher = $null

    for ($i = 0; $i -lt 2880; $i++) {
        $ym = $t.Year * 100 + $t.Month
        if ($ym -ne $curMonth) {
            $matcher = New-CronMatcher -Cron $Cron -ContextDate $t
            $curMonth = $ym
        }
        if (Test-CronMatch -Dt $t -Matcher $matcher) { return $t }
        $t = $t.AddMinutes(-1)
    }
    return $null
}

function Get-NextCronTime {
    param([datetime]$Now, $Cron, [switch]$IncludeCurrent)
    if ($IncludeCurrent) {
        $t = (Get-Date $Now).AddSeconds(-$Now.Second).AddMilliseconds(-$Now.Millisecond)
    }
    else {
        $t = (Get-Date $Now).AddSeconds(-$Now.Second).AddMilliseconds(-$Now.Millisecond).AddMinutes(1)
    }

    for ($day = 0; $day -lt 370; $day++) {
        $matcher = New-CronMatcher -Cron $Cron -ContextDate $t
        if ($matcher.Months.Contains($t.Month)) {
            $domMatch = $matcher.Dom.Contains($t.Day)
            $dowMatch = $matcher.Dow.Contains([int]$t.DayOfWeek)
            $dayMatches = if ($matcher.DomIsWildcard -and $matcher.DowIsWildcard) { $true }
                elseif ($matcher.DomIsWildcard) { $dowMatch }
                elseif ($matcher.DowIsWildcard) { $domMatch }
                else { $domMatch -or $dowMatch }

            if ($dayMatches) {
                $startHour = if ($day -eq 0) { $t.Hour } else { 0 }
                for ($h = $startHour; $h -lt 24; $h++) {
                    if (-not $matcher.Hours.Contains($h)) { continue }
                    $startMin = if ($day -eq 0 -and $h -eq $t.Hour) { $t.Minute } else { 0 }
                    for ($m = $startMin; $m -lt 60; $m++) {
                        if ($matcher.Minutes.Contains($m)) {
                            return [datetime]::new($t.Year, $t.Month, $t.Day, $h, $m, 0, $t.Kind)
                        }
                    }
                }
            }
        }
        $t = [datetime]::new($t.Year, $t.Month, $t.Day, 0, 0, 0, $t.Kind).AddDays(1)
    }
    return $null
}

# ===================== timeout 강제 종료 =====================
function Stop-ProcessTree {
    param([int]$ProcessId)
    try {
        & "$env:SystemRoot\System32\taskkill.exe" /PID $ProcessId /T /F | Out-Null
        return $true
    }
    catch {
        try { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue; return $true } catch { return $false }
    }
}

function Start-TimeoutWatchdog {
    param(
        [int]$ProcessId,
        [int]$TimeoutSec,
        [string]$CompletionFile,
        [string]$JobBaseName,
        [string]$RunId
    )

    if ($TimeoutSec -le 0) { return }

    Start-Job -ScriptBlock {
        param($ProcessId, $TimeoutSec, $CompletionFile, $JobBaseName, $RunId)

        Start-Sleep -Seconds $TimeoutSec

        $alive = $false
        try { Get-Process -Id $ProcessId -ErrorAction Stop | Out-Null; $alive = $true } catch { $alive = $false }

        if ($alive) {
            try {
                & "$env:SystemRoot\System32\taskkill.exe" /PID $ProcessId /T /F | Out-Null
            }
            catch {
                try { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue } catch {}
            }

            $obj = [pscustomobject]@{
                JobName  = $JobBaseName
                RunId    = $RunId
                Started  = $null
                Ended    = (Get-Date)
                ExitCode = 124
                Message  = "타임아웃($TimeoutSec초) 초과로 강제 종료"
                Timeout  = $true
            }
            ($obj | ConvertTo-Json -Depth 5) | Set-Content -Path $CompletionFile -Encoding UTF8
        }
    } -ArgumentList $ProcessId, $TimeoutSec, $CompletionFile, $JobBaseName, $RunId | Out-Null
}

# ===================== runner(ExitCode 기록) =====================
function New-RunnerScript {
    param(
        [string]$RunnerPath,
        [string]$JobPath,
        [string]$CompletionPath,
        [string]$OutputLogPath,
        [string]$JobBaseName,
        [string]$RunId,
        [bool]$JobLogOutputOnSuccess
    )

    $template = @'
$ErrorActionPreference = 'Stop'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$jobPath = '__JOB_PATH__'
$completion = '__COMPLETION_PATH__'
$outLog = '__OUTPUT_LOG__'
$jobName = '__JOB_NAME__'
$runId = '__RUN_ID__'
$logOutputOnSuccess = '__LOG_OUTPUT_ON_SUCCESS__'

$shouldLogOutputOnSuccess = $false
try {
    if ($logOutputOnSuccess -is [bool]) { $shouldLogOutputOnSuccess = [bool]$logOutputOnSuccess }
    else {
        $t = ("$logOutputOnSuccess").Trim().ToUpperInvariant()
        $shouldLogOutputOnSuccess = ($t -eq '1' -or $t -eq 'TRUE')
    }
} catch { $shouldLogOutputOnSuccess = $false }

$outputBuffer = New-Object 'System.Collections.Generic.List[string]'

function Write-OutputLogLine {
    param(
        [ValidateSet('정보','경고','오류')] [string]$Level,
        [string]$Text
    )

    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $prefix = "[$ts][$Level][JOB:${jobName}:$runId]"

    if ($null -eq $Text) { $Text = '' }

    foreach ($l in ($Text -split "`r?`n")) {
        [void]$outputBuffer.Add(("$prefix $l"))
    }
}

function Flush-OutputLog {
    if ($outputBuffer.Count -le 0) { return }
    try {
        $dir = Split-Path -Parent $outLog
        if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
        Add-Content -Path $outLog -Encoding UTF8 -Value $outputBuffer
    } catch {}
}

function Write-OutputLogObject {
    param($Obj)

    if ($Obj -is [System.Management.Automation.ErrorRecord]) {
        Write-OutputLogLine -Level '오류' -Text ($Obj.ToString())
        return
    }
    if ($Obj -is [System.Management.Automation.WarningRecord]) {
        Write-OutputLogLine -Level '경고' -Text ($Obj.Message)
        return
    }
    if ($Obj -is [System.Management.Automation.InformationRecord]) {
        $msg = $Obj.MessageData
        if ($null -ne $msg) {
            ($msg | Out-String -Stream) | ForEach-Object { Write-OutputLogLine -Level '정보' -Text $_ }
        }
        return
    }

    ($Obj | Out-String -Stream) | ForEach-Object { Write-OutputLogLine -Level '정보' -Text $_ }
}

function global:Write-Host {
    [CmdletBinding()]
    param(
        [Parameter(Position=0, ValueFromPipeline=$true)]
        [object]$Object,
        [ConsoleColor]$ForegroundColor,
        [ConsoleColor]$BackgroundColor,
        [object]$Separator = ' ',
        [switch]$NoNewline
    )
    process {
        $text = ''
        if ($null -eq $Object) {
            $text = ''
        } elseif ($Object -is [string]) {
            $text = $Object
        } elseif ($Object -is [System.Collections.IEnumerable]) {
            try { $text = (@($Object) | ForEach-Object { "$_" }) -join ("$Separator") } catch { $text = ("$Object") }
        } else {
            $text = ("$Object")
        }
        Write-OutputLogLine -Level '정보' -Text $text
    }
}

$start = Get-Date
$exitCode = 0
$msg = '성공'
try {
    & $jobPath *>&1 | ForEach-Object { Write-OutputLogObject $_ }
    if ($LASTEXITCODE -ne $null) { $exitCode = [int]$LASTEXITCODE } else { $exitCode = 0 }
    if ($exitCode -ne 0) { throw "ExitCode=$exitCode" }
}
catch {
    $exitCode = if ($exitCode -ne 0) { $exitCode } else { 1 }
    $msg = "실패: $($_.Exception.Message)"
    Write-OutputLogLine -Level '오류' -Text $msg
}
finally {
    if (-not (Test-Path $completion)) {
        $end = Get-Date
        $obj = [pscustomobject]@{
            JobName = $jobName
            RunId = $runId
            Started = $start
            Ended = $end
            ExitCode = $exitCode
            Message = $msg
            Timeout = $false
        }
        ($obj | ConvertTo-Json -Depth 5) | Set-Content -Path $completion -Encoding UTF8
    }

    if ($exitCode -ne 0 -or $shouldLogOutputOnSuccess) {
        Flush-OutputLog
    }
}
exit $exitCode
'@

    $content = $template
    $content = $content.Replace('__JOB_PATH__', $JobPath.Replace("'", "''"))
    $content = $content.Replace('__COMPLETION_PATH__', $CompletionPath.Replace("'", "''"))
    $content = $content.Replace('__OUTPUT_LOG__', $OutputLogPath.Replace("'", "''"))
    $content = $content.Replace('__JOB_NAME__', $JobBaseName.Replace("'", "''"))
    $content = $content.Replace('__RUN_ID__', $RunId.Replace("'", "''"))
    $content = $content.Replace('__LOG_OUTPUT_ON_SUCCESS__', ($(if ($JobLogOutputOnSuccess) { 'TRUE' } else { 'FALSE' })))

    Set-Content -Path $RunnerPath -Value $content -Encoding UTF8 -WhatIf:$false -Confirm:$false
}

# ===================== 작업 실행 =====================
function Start-JobWithRunner {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [string]$JobFullPath,
        [string]$JobBaseName,
        [string]$RunId,
        [int]$TimeoutSec,
        [bool]$LogOpt
    )

    if (-not $PSCmdlet.ShouldProcess($JobFullPath, "스케줄 작업 실행 (Job: $JobBaseName, RunId: $RunId, Timeout: ${TimeoutSec}s)")) {
        return [pscustomobject]@{
            Pid        = 0
            Completion = ''
            OutputLog  = ''
            Simulated  = $true
        }
    }

    $lock = Get-LockPath $JobFullPath
    if (Test-Path $lock) { return $null }
    New-Item -ItemType File -Path $lock -Force | Out-Null

    $jobLogDir = Get-JobLogDir -JobBaseName $JobBaseName
    New-Item -ItemType Directory -Force -Path $jobLogDir | Out-Null

    $outputLog = Join-Path $jobLogDir '출력.log'

    $completion = Get-CompletionPath -JobFullPath $JobFullPath -RunId $RunId
    $runnerPath = Join-Path $StateDir ("runner_" + (Get-JobKey $JobFullPath) + ".ps1")
    $useLogOpt = if ($LogOpt) { $true } else { $LogOutputOnSuccess }
    New-RunnerScript -RunnerPath $runnerPath -JobPath $JobFullPath -CompletionPath $completion -OutputLogPath $outputLog -JobBaseName $JobBaseName -RunId $RunId -JobLogOutputOnSuccess $useLogOpt

    $psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $psArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$runnerPath`"")

    Write-SchedulerLog "작업 실행 시작: $JobBaseName (RunId=$RunId, Timeout=${TimeoutSec}s)"
    Write-JobLog -JobBaseName $JobBaseName -RunId $RunId -Message "작업 실행 시작: $JobFullPath / Timeout=${TimeoutSec}s"

    try {
        $p = Start-Process -FilePath $psExe -ArgumentList $psArgs -PassThru -WindowStyle Hidden

        $state = Get-JobState -JobFullPath $JobFullPath
        $state.PendingPid = $p.Id
        $state.PendingStartTime = $p.StartTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
        $state.PendingRunId = $RunId
        Save-JobState -JobFullPath $JobFullPath -StateObj $state

        Start-TimeoutWatchdog -ProcessId $p.Id -TimeoutSec $TimeoutSec -CompletionFile $completion -JobBaseName $JobBaseName -RunId $RunId

        Start-Job -ScriptBlock {
            param($ProcessId, $LockPath)
            try {
                $proc = [System.Diagnostics.Process]::GetProcessById($ProcessId)
                $proc.WaitForExit()
            }
            catch {}
            finally { Remove-Item -Path $LockPath -Force -ErrorAction SilentlyContinue }
        } -ArgumentList $p.Id, $lock | Out-Null

        return [pscustomobject]@{
            Pid        = $p.Id
            Completion = $completion
            OutputLog  = $outputLog
            Simulated  = $false
        }
    }
    catch {
        Remove-Item -Path $lock -Force -ErrorAction SilentlyContinue
        Write-SchedulerLog "작업 시작 실패: $JobBaseName / $($_.Exception.Message)" '오류'
        Write-JobLog -JobBaseName $JobBaseName -RunId $RunId -Level '오류' -Message "작업 시작 실패: $($_.Exception.Message)"
        return $null
    }
}

# ===================== completion 반영 + 서킷 =====================
function Open-Circuit {
    param([ref]$StateRef, [int]$CoolDownMin)
    $state = $StateRef.Value
    $state.CircuitOpenUntil = (Get-Date).AddMinutes($CoolDownMin)
    $state.NextRetry = $null
}

function Set-CompletionState {
    param(
        [string]$JobPath,
        [string]$JobBaseName,
        [string]$CompletionFile,
        [ref]$StateRef,
        [ref]$JobDefRef
    )

    $state = $StateRef.Value
    $jobDef = $JobDefRef.Value

    $obj = $null
    try { $obj = Get-Content -Raw -Path $CompletionFile | ConvertFrom-Json }
    catch { Write-SchedulerLog "완료 파일 파싱 실패: $CompletionFile" '경고'; return }

    $exitCode = [int]$obj.ExitCode
    $msg = [string]$obj.Message
    $isTimeout = $false
    try { $isTimeout = [bool]$obj.Timeout } catch { $isTimeout = $false }

    $state.LastExitCode = $exitCode
    $state.PendingPid = $null
    $state.PendingRunId = $null

    if ($exitCode -eq 0) {
        $state.RetryCount = 0
        $state.NextRetry = $null
        $state.ConsecutiveFailures = 0
        $state.CircuitOpenUntil = $null
        $state.NextRun = Get-NextCronTime -Now (Get-Date) -Cron $jobDef.Cron

        Write-SchedulerLog "작업 성공: $JobBaseName (ExitCode=0)" '정보'
        Write-JobLog -JobBaseName $JobBaseName -RunId $obj.RunId -Message "작업 성공: $msg"
        Write-Event "작업 성공: $JobBaseName. $msg" 'Information' 3200
    }
    else {
        $state.ConsecutiveFailures = [int]$state.ConsecutiveFailures + 1

        $detail = "ExitCode=$exitCode / $msg"
        if ($isTimeout) { $detail = "타임아웃 발생 / " + $detail }

        Write-SchedulerLog "작업 실패: $JobBaseName / $detail (연속실패=$($state.ConsecutiveFailures))" '오류'
        Write-JobLog -JobBaseName $JobBaseName -RunId $obj.RunId -Level '오류' -Message "작업 실패: $detail / 연속실패=$($state.ConsecutiveFailures)"

        if ($isTimeout) {
            Write-Event "타임아웃: $JobBaseName (Timeout=${jobDef.TimeoutSec}s). $detail" 'Error' 3201
        }
        else {
            Write-Event "작업 실패: $JobBaseName. $detail" 'Error' 3202
        }

        $cN = [int]$jobDef.CircuitN
        if ($cN -gt 0 -and $state.ConsecutiveFailures -ge $cN) {
            Open-Circuit -StateRef ([ref]$state) -CoolDownMin $DefaultCircuitCoolDownMin
            Write-SchedulerLog "서킷브레이커 OPEN: $JobBaseName / $DefaultCircuitCoolDownMin 분 동안 차단" '경고'
            Write-JobLog -JobBaseName $JobBaseName -RunId $obj.RunId -Level '경고' -Message "서킷브레이커 OPEN: $DefaultCircuitCoolDownMin 분 차단"
            Write-Event "서킷브레이커 OPEN: $JobBaseName. 연속 실패 $($state.ConsecutiveFailures)회로 ${DefaultCircuitCoolDownMin}분 차단." 'Error' 3301

            $state.NextRun = Get-NextCronTime -Now (Get-Date) -Cron $jobDef.Cron
            $state.RetryCount = 0
            $state.NextRetry = $null
        }
        else {
            if ($RetryEnabled -and $state.RetryCount -lt $MaxRetries) {
                $delayMin = $RetryBackoffMin[$state.RetryCount]
                $state.RetryCount = [int]$state.RetryCount + 1
                $state.NextRetry = (Get-Date).AddMinutes($delayMin)
                Write-SchedulerLog "재시도 예약: $JobBaseName / ${delayMin}분 후 (RetryCount=$($state.RetryCount))" '경고'
                Write-JobLog -JobBaseName $JobBaseName -RunId $obj.RunId -Level '경고' -Message "재시도 예약: ${delayMin}분 후"
            }
            else {
                $state.NextRetry = $null
                $state.NextRun = Get-NextCronTime -Now (Get-Date) -Cron $jobDef.Cron
                Write-SchedulerLog "재시도 한도 초과: $JobBaseName / 다음 스케줄 대기" '경고'
            }
        }
    }

    Remove-Item -Path $CompletionFile -Force -Confirm:$false -ErrorAction SilentlyContinue
}

# ===================== 메인 =====================
Convert-TodaysLogsToUtf8

if (Test-Path -LiteralPath $StopFile) {
    try { Remove-Item -LiteralPath $StopFile -Force -Confirm:$false -ErrorAction SilentlyContinue } catch {}
}

# 고아 runner 스크립트 정리
Get-ChildItem -Path $StateDir -Filter 'runner_*.ps1' -File -ErrorAction SilentlyContinue |
    ForEach-Object { try { Remove-Item -LiteralPath $_.FullName -Force -Confirm:$false -ErrorAction SilentlyContinue } catch {} }

Write-SchedulerLog "스케줄러 시작. JobsDir=$JobsDir"
Write-SchedulerLog "중지 감시 파일: $StopFile"
Write-Event "스케줄러 시작. JobsDir=$JobsDir" 'Information' 3001

$shutdownRequested = $false
$lastShutdownLockCount = -1

while ($true) {
    Remove-OldLogs -RetentionDays $LogRetentionDays
    Remove-OldItems -Path $StateDir -RetentionDays $StateRetentionDays
    Get-Job -State Completed -ErrorAction SilentlyContinue | Remove-Job -Force -ErrorAction SilentlyContinue

    $now = Get-Date

    # 완료 파일 처리
    $completeFiles = Get-ChildItem -Path $StateDir -Filter 'complete_*.json' -File -ErrorAction SilentlyContinue
    foreach ($cf in $completeFiles) {
        try {
            if ($cf.Name -match '^complete_([0-9a-f]+)_(.+)\.json$') {
                $jobKey = $Matches[1]
                $jobFilesForKey = Get-ChildItem -Path $JobsDir -Filter '*.ps1' -File -ErrorAction SilentlyContinue | Where-Object {
                    (Get-JobKey $_.FullName) -eq $jobKey
                }

                if (-not $jobFilesForKey) {
                    Write-SchedulerLog "고아 완료 파일 삭제(원본 작업 없음): $($cf.FullName)" '정보'
                    Remove-Item -Path $cf.FullName -Force -Confirm:$false -ErrorAction SilentlyContinue
                    continue
                }

                foreach ($jf in $jobFilesForKey) {
                    $jobPath = $jf.FullName
                    $base = $jf.BaseName

                    $jobDef = ConvertFrom-JobFileName -BaseNameNoExt $base
                    if ($null -eq $jobDef) { continue }

                    $state = Get-JobState -JobFullPath $jobPath
                    Set-CompletionState -JobPath $jobPath -JobBaseName $base -CompletionFile $cf.FullName -StateRef ([ref]$state) -JobDefRef ([ref]$jobDef)
                    Save-JobState -JobFullPath $jobPath -StateObj $state
                }
            }
        }
        catch {
            Write-SchedulerLog "완료 파일 처리 오류: $($cf.FullName) / $($_.Exception.Message)" '경고'
        }
    }

    $runningLocks = Get-ActiveLockCount

    if ([System.IO.File]::Exists($StopFile)) {
        if (-not $shutdownRequested) {
            $shutdownRequested = $true
            Write-SchedulerLog '중지 파일 감지. 새 작업은 중단하고 실행 중인 작업 종료를 대기합니다.'
            Write-Event '중지 파일 감지. 실행 중인 작업 종료 대기.' 'Information' 3002
        }

        if ($runningLocks -le 0) {
            break
        }

        if ($lastShutdownLockCount -ne $runningLocks) {
            Write-SchedulerLog "종료 대기 중: 실행 중 작업 ${runningLocks}개"
            $lastShutdownLockCount = $runningLocks
        }

        Start-Sleep -Seconds $TickSeconds
        continue
    }

    # jobs 스캔
    $jobFiles = Get-ChildItem -Path $JobsDir -Filter '*.ps1' -File -ErrorAction SilentlyContinue

    foreach ($jf in $jobFiles) {
        $jobPath = $jf.FullName
        $base = $jf.BaseName

        $jobDef = ConvertFrom-JobFileName -BaseNameNoExt $base
        if ($null -eq $jobDef) { continue }

        $state = Get-JobState -JobFullPath $jobPath

        # 서킷 오픈 체크
        if ($state.CircuitOpenUntil) {
            $until = [datetime]$state.CircuitOpenUntil
            if ($now -lt $until) {
                continue
            }
            else {
                $state.CircuitOpenUntil = $null
                $state.ConsecutiveFailures = 0
                Save-JobState -JobFullPath $jobPath -StateObj $state
                Write-SchedulerLog "서킷브레이커 CLOSE: $base" '정보'
            }
        }

        # NOW=1 즉시 실행(1회) 처리
        if ($jobDef.NowOpt -and (-not $state.NowConsumed)) {
            $reason = '즉시실행(NOW=1)'
            $shouldRun = $true
        }
        else {
            $shouldRun = $false
            $reason = ''
        }

        # next run 계산
        if (-not $state.NextRun) {
            $prevRun = Get-PrevCronTime -Now $now -Cron $jobDef.Cron

            if ($prevRun -and $prevRun.ToString('yyyyMMddHHmm') -eq $now.ToString('yyyyMMddHHmm')) {
                $shouldRun = $true
                $reason = '최초실행(현재시각일치)'
                $state.NextRun = Get-NextCronTime -Now $now -Cron $jobDef.Cron
            }
            else {
                $state.NextRun = Get-NextCronTime -Now $now -Cron $jobDef.Cron -IncludeCurrent
            }
            Save-JobState -JobFullPath $jobPath -StateObj $state
        }

        # 재시도/스케줄 트리거
        if (-not $shouldRun) {
            if ($RetryEnabled -and $state.NextRetry) {
                if ($now -ge [datetime]$state.NextRetry) { $shouldRun = $true; $reason = '재시도' }
            }
            else {
                if ($state.NextRun -and $now -ge [datetime]$state.NextRun) { $shouldRun = $true; $reason = '스케줄' }
            }
        }

        if (-not $shouldRun) { continue }

        # 병렬 제한
        if ($runningLocks -ge $MaxParallel) {
            Write-SchedulerLog "병렬 실행 한도 도달($runningLocks/$MaxParallel). 실행 스킵: $base" '경고'
            Write-JobLog -JobBaseName $base -Level '경고' -Message '병렬 실행 한도 도달로 스킵'
            continue
        }

        # lock 체크 (CONCURRENT 옵션이 켜져 있으면 스킵)
        if (-not $jobDef.Concurrent) {
            if (Test-Path (Get-LockPath $jobPath)) { continue }
        }

        $runId = (Get-Date -Format 'HHmmss') + '_' + ([Guid]::NewGuid().ToString('N').Substring(0, 6))
        $state.LastRun = $now

        Write-JobLog -JobBaseName $base -RunId $runId -Message "트리거: $reason / Timeout=$($jobDef.TimeoutSec)s / CB=$($jobDef.CircuitN)"
        Write-Event "작업 시작: $base. RunId=$runId, 상태: $reason" 'Information' 3100

        $started = Start-JobWithRunner -JobFullPath $jobPath -JobBaseName $base -RunId $runId -TimeoutSec ([int]$jobDef.TimeoutSec) -LogOpt $jobDef.LogOpt

        if ($null -eq $started) {
            $state.LastExitCode = 9999
            $state.ConsecutiveFailures = [int]$state.ConsecutiveFailures + 1

            Write-SchedulerLog "작업 시작 실패: $base (연속실패=$($state.ConsecutiveFailures))" '오류'
            Write-Event "작업 시작 실패: $base" 'Error' 3103

            if ([int]$jobDef.CircuitN -gt 0 -and $state.ConsecutiveFailures -ge [int]$jobDef.CircuitN) {
                Open-Circuit -StateRef ([ref]$state) -CoolDownMin $DefaultCircuitCoolDownMin
                Write-SchedulerLog "서킷브레이커 OPEN: $base / ${DefaultCircuitCoolDownMin}분 차단" '경고'
                Write-Event "서킷브레이커 OPEN: $base. 연속 실패로 ${DefaultCircuitCoolDownMin}분 차단." 'Error' 3301
                $state.RetryCount = 0
                $state.NextRetry = $null
            }
            else {
                if ($RetryEnabled -and $state.RetryCount -lt $MaxRetries) {
                    $delayMin = $RetryBackoffMin[$state.RetryCount]
                    $state.RetryCount = [int]$state.RetryCount + 1
                    $state.NextRetry = (Get-Date).AddMinutes($delayMin)
                }
                else {
                    $state.NextRetry = $null
                }
            }
        }
        elseif (-not $started.Simulated) {
            $state.PendingPid = $started.Pid
            $state.PendingRunId = $runId
            $runningLocks++

            if ($reason -like '즉시실행*') { $state.NowConsumed = $true }
            $state.NextRetry = $null
            $state.NextRun = Get-NextCronTime -Now $now -Cron $jobDef.Cron
        }

        Save-JobState -JobFullPath $jobPath -StateObj $state
    }

    if ($RunOnce) {
        Write-SchedulerLog 'RunOnce 옵션에 의해 1회 루프 수행 후 종료합니다.'
        break
    }

    Start-Sleep -Seconds $TickSeconds
}

if (Test-Path -LiteralPath $StopFile) {
    try { Remove-Item -LiteralPath $StopFile -Force -Confirm:$false -ErrorAction SilentlyContinue } catch {}
}

Write-SchedulerLog '스케줄러 종료.'
Write-Event '스케줄러 종료.' 'Information' 3003
