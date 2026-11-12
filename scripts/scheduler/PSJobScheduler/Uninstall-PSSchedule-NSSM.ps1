#Requires -Version 5.1
<#
.SYNOPSIS
    NSSM으로 등록된 PSJobScheduler Windows 서비스를 안전하게 중지하고 제거합니다.

.DESCRIPTION
    schedule.stop 파일을 생성하여 Graceful Shutdown을 먼저 유도한 뒤,
    nssm.exe를 통해 서비스를 중지하고 등록 해제합니다. (-WhatIf 지원)

.PARAMETER ServiceName
    제거할 Windows 서비스 이름입니다 (기본값: PSJobScheduler).

.PARAMETER BaseDir
    스케줄러 및 nssm.exe가 위치한 기본 디렉터리입니다.

.EXAMPLE
    .\Uninstall-PSSchedule-NSSM.ps1 -WhatIf
    서비스 제거 과정을 시뮬레이션합니다.

.EXAMPLE
    .\Uninstall-PSSchedule-NSSM.ps1
    PSJobScheduler 서비스를 중지하고 제거합니다 (관리자 권한 필요).
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(HelpMessage = '제거할 Windows 서비스 이름')]
    [ValidateNotNullOrEmpty()]
    [string]$ServiceName = 'PSJobScheduler',

    [Parameter(HelpMessage = '스케줄러 기본 디렉터리 경로')]
    [string]$BaseDir = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BaseDir) {
    $BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$NssmPath = Join-Path $BaseDir 'nssm.exe'
$StopFile = Join-Path $BaseDir 'schedule.stop'

if (-not (Test-Path -LiteralPath $NssmPath)) { throw "NSSM not found: $NssmPath" }

if (-not $WhatIfPreference) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw '서비스 제거는 관리자 권한으로 실행해야 합니다.'
    }
}

if ($PSCmdlet.ShouldProcess($ServiceName, 'Graceful Shutdown 요청 후 NSSM 서비스 중지 및 제거')) {
    New-Item -ItemType File -Path $StopFile -Force -Confirm:$false | Out-Null
    Start-Sleep -Seconds 5

    & $NssmPath stop $ServiceName | Out-Null
    & $NssmPath remove $ServiceName confirm | Out-Null

    if (Test-Path -LiteralPath $StopFile) {
        Remove-Item -LiteralPath $StopFile -Force -Confirm:$false -ErrorAction SilentlyContinue
    }

    Write-Host "Uninstalled service: $ServiceName" -ForegroundColor Green
}
