#Requires -Version 5.1
<#
.SYNOPSIS
    NSSM(nssm.exe)을 사용하여 PSJobScheduler를 Windows 서비스로 등록 및 시작합니다.

.DESCRIPTION
    1. Windows 이벤트 로그(PSJobScheduler-Logs) 및 소스(PSJobScheduler)를 점검/등록합니다.
    2. 기존 동명 서비스가 존재하면 schedule.stop을 통해 안전하게 중지 후 재설치합니다.
    3. nssm.exe를 통해 schedule-service-host.ps1를 서비스로 등록하고 로그 로테이션(10MB)을 설정한 뒤 시작합니다.
    4. -WhatIf 옵션을 통해 실제 서비스/이벤트로그 변경 없이 설치 과정을 시뮬레이션할 수 있습니다.

.PARAMETER ServiceName
    등록할 Windows 서비스 이름입니다 (기본값: PSJobScheduler).

.PARAMETER DisplayName
    Windows 서비스 표시 이름입니다 (기본값: PowerShell Job Scheduler (Cron-Safe)).

.PARAMETER Description
    Windows 서비스 설명입니다.

.PARAMETER EventLogName
    생성할 이벤트 로그 이름입니다 (기본값: PSJobScheduler-Logs).

.PARAMETER EventSource
    생성할 이벤트 소스 이름입니다 (기본값: PSJobScheduler).

.PARAMETER BaseDir
    스케줄러 및 nssm.exe가 위치한 기본 디렉터리입니다.

.EXAMPLE
    .\Install-PSSchedule-NSSM.ps1 -WhatIf
    서비스 설치 작업을 시뮬레이션합니다.

.EXAMPLE
    .\Install-PSSchedule-NSSM.ps1
    PSJobScheduler 서비스를 등록하고 시작합니다 (관리자 권한 필요).
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(HelpMessage = 'Windows 서비스 이름')]
    [ValidateNotNullOrEmpty()]
    [string]$ServiceName = 'PSJobScheduler',

    [Parameter(HelpMessage = 'Windows 서비스 표시 이름')]
    [ValidateNotNullOrEmpty()]
    [string]$DisplayName = 'PowerShell Job Scheduler (Cron-Safe)',

    [Parameter(HelpMessage = 'Windows 서비스 설명')]
    [string]$Description = 'Cron-Safe 파일명 기반 PowerShell 작업 스케줄러 서비스',

    [Parameter(HelpMessage = 'Windows 이벤트 로그 이름')]
    [ValidateNotNullOrEmpty()]
    [string]$EventLogName = 'PSJobScheduler-Logs',

    [Parameter(HelpMessage = 'Windows 이벤트 소스 이름')]
    [ValidateNotNullOrEmpty()]
    [string]$EventSource = 'PSJobScheduler',

    [Parameter(HelpMessage = '스케줄러 기본 디렉터리 경로')]
    [string]$BaseDir = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BaseDir) {
    $BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$HostScriptPath = Join-Path $BaseDir 'schedule-service-host.ps1'
$SchedulerPath = Join-Path $BaseDir 'schedule.ps1'
$StopFile = Join-Path $BaseDir 'schedule.stop'
$NssmPath = Join-Path $BaseDir 'nssm.exe'
$PowershellExe = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"

if (-not (Test-Path -LiteralPath $NssmPath)) { throw "NSSM not found: $NssmPath" }
if (-not (Test-Path -LiteralPath $HostScriptPath)) { throw "Host script not found: $HostScriptPath" }
if (-not (Test-Path -LiteralPath $SchedulerPath)) { throw "Scheduler not found: $SchedulerPath" }

if (-not $WhatIfPreference) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw '서비스 설치는 관리자 권한으로 실행해야 합니다.'
    }
}

if (Test-Path -LiteralPath $StopFile) {
    if ($PSCmdlet.ShouldProcess($StopFile, '기존 schedule.stop 파일 제거')) {
        Remove-Item -LiteralPath $StopFile -Force -Confirm:$false -ErrorAction SilentlyContinue
    }
}

# Event Source 등록
if ($PSCmdlet.ShouldProcess("EventLog: $EventLogName / Source: $EventSource", 'Windows 이벤트 로그 소스 확인 및 등록')) {
    try {
        Write-Host "Event Source($EventSource) 설정 확인 중..."

        if ([System.Diagnostics.EventLog]::SourceExists($EventSource)) {
            $currentLog = [System.Diagnostics.EventLog]::LogNameFromSourceName($EventSource, '.')
            if ($currentLog -ne $EventLogName) {
                Write-Host "기존 소스가 다른 로그($currentLog)에 등록되어 있어 제거합니다."
                [System.Diagnostics.EventLog]::DeleteEventSource($EventSource)
            }
        }

        if (-not [System.Diagnostics.EventLog]::SourceExists($EventSource)) {
            Write-Host "새 이벤트 로그($EventLogName) 및 소스($EventSource) 생성 중..."
            New-EventLog -LogName $EventLogName -Source $EventSource
        }
        else {
            Write-Host "Event Source 이미 올바르게 등록됨 ($EventLogName)"
        }
    }
    catch {
        Write-Warning "이벤트 로그 등록 실패: $($_.Exception.Message)"
    }
}

# 기존 서비스 있으면 제거
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($PSCmdlet.ShouldProcess($ServiceName, '기존 동명 서비스 중지 및 제거')) {
        New-Item -ItemType File -Path $StopFile -Force -Confirm:$false | Out-Null
        Start-Sleep -Seconds 5
        & $NssmPath stop $ServiceName | Out-Null
        & $NssmPath remove $ServiceName confirm | Out-Null
    }
}

# NSSM으로 서비스 생성 및 시작
if ($PSCmdlet.ShouldProcess($ServiceName, "NSSM 서비스 설치 및 시작 ($HostScriptPath)")) {
    & $NssmPath install $ServiceName $PowershellExe `
        "-NoProfile -ExecutionPolicy Bypass -File `"$HostScriptPath`"" | Out-Null

    & $NssmPath set $ServiceName DisplayName $DisplayName | Out-Null
    & $NssmPath set $ServiceName Description $Description | Out-Null
    & $NssmPath set $ServiceName AppDirectory $BaseDir | Out-Null
    & $NssmPath set $ServiceName Start SERVICE_AUTO_START | Out-Null

    $LogDir = Join-Path $BaseDir 'logs'
    $StdOut = Join-Path $LogDir 'service-stdout.log'
    $StdErr = Join-Path $LogDir 'service-stderr.log'
    New-Item -ItemType Directory -Path $LogDir -Force -Confirm:$false | Out-Null
    & $NssmPath set $ServiceName AppStdout $StdOut | Out-Null
    & $NssmPath set $ServiceName AppStderr $StdErr | Out-Null
    & $NssmPath set $ServiceName AppRotateFiles 1 | Out-Null
    & $NssmPath set $ServiceName AppRotateOnline 1 | Out-Null
    & $NssmPath set $ServiceName AppRotateBytes (10MB) | Out-Null

    & $NssmPath set $ServiceName AppStopMethodSkip 0 | Out-Null
    & $NssmPath set $ServiceName AppStopMethodConsole 15000 | Out-Null
    & $NssmPath set $ServiceName AppStopMethodWindow 15000 | Out-Null
    & $NssmPath set $ServiceName AppStopMethodThreads 15000 | Out-Null
    & $NssmPath set $ServiceName AppStopMethodTerminate 15000 | Out-Null

    Start-Service -Name $ServiceName -Confirm:$false
    Write-Host "Installed and started service: $ServiceName" -ForegroundColor Green
}
