#Requires -Version 5.1
<#
.SYNOPSIS
    NSSM Windows 서비스 구동을 위한 PSJobScheduler 호스트 래퍼 스크립트입니다.

.DESCRIPTION
    Windows 서비스(NSSM)에서 schedule.ps1 프로세스를 실행하고 감시하며,
    서비스 중지 시 schedule.stop 파일을 생성하여 Graceful Shutdown을 유도합니다.

.PARAMETER BaseDir
    스케줄러 기본 디렉터리 경로입니다 (기본값: 스크립트 위치).

.PARAMETER ShutdownTimeoutSec
    서비스 중지 요청 시 실행 중인 작업 완료를 기다릴 최대 대기 시간(초)입니다 (기본값: 30).
#>
[CmdletBinding()]
param(
    [Parameter(HelpMessage = '스케줄러 기본 디렉터리 경로')]
    [string]$BaseDir = $PSScriptRoot,

    [Parameter(HelpMessage = 'Graceful Shutdown 최대 대기 시간(초)')]
    [int]$ShutdownTimeoutSec = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BaseDir) {
    $BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$SchedulerPath = Join-Path $BaseDir 'schedule.ps1'
$StopFile = Join-Path $BaseDir 'schedule.stop'
$PowerShellExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$child = $null

function Stop-ProcessTree {
    param([int]$ProcessId)

    if ($ProcessId -le 0) { return }

    try {
        & "$env:SystemRoot\System32\taskkill.exe" /PID $ProcessId /T /F | Out-Null
    }
    catch {
        try { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Request-SchedulerStop {
    if (-not (Test-Path -LiteralPath $StopFile)) {
        New-Item -ItemType File -Path $StopFile -Force | Out-Null
    }
}

if (-not (Test-Path -LiteralPath $SchedulerPath)) {
    throw "Scheduler script not found: $SchedulerPath"
}

if (Test-Path -LiteralPath $StopFile) {
    Remove-Item -LiteralPath $StopFile -Force -ErrorAction SilentlyContinue
}

try {
    $child = Start-Process -FilePath $PowerShellExe -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', "`"$SchedulerPath`""
    ) -PassThru -WindowStyle Hidden

    while ($true) {
        $child.Refresh()
        if ($child.HasExited) { break }
        Start-Sleep -Seconds 2
    }

    exit $child.ExitCode
}
finally {
    if ($child -and (-not $child.HasExited)) {
        Request-SchedulerStop

        $deadline = (Get-Date).AddSeconds($ShutdownTimeoutSec)
        while ((Get-Date) -lt $deadline) {
            try { $child.Refresh() } catch {}
            if ($child.HasExited) { break }
            Start-Sleep -Seconds 1
        }

        if (-not $child.HasExited) {
            Stop-ProcessTree -ProcessId $child.Id
        }
    }
}
