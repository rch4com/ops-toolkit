#Requires -Version 5.1
<#
.SYNOPSIS
    PSJobScheduler에 등록된 작업(jobs)들의 현재 실행 상태 및 스케줄 대시보드를 출력합니다.

.DESCRIPTION
    jobs 폴더의 .ps1 파일들과 state 폴더의 상태(JSON) 및 Lock 파일을 대조하여
    각 작업의 상태(IDLE, RUNNING, RUNNING(STALE?), CIRCUIT_OPEN), 중복 실행 허용 여부,
    마지막 실행 시각, 다음 예정 시각, 최근 종료 코드, 연속 실패 횟수를 표시합니다.

.PARAMETER BaseDir
    스케줄러 기본 디렉터리 경로입니다 (기본값: 스크립트 위치).

.PARAMETER PassThru
    콘솔 출력 외에 작업 상태 객체(PSCustomObject[])를 파이프라인으로 반환합니다.

.EXAMPLE
    .\Get-JobStatus.ps1
    작업 상태 대시보드를 콘솔에 테이블 형태로 출력합니다.

.EXAMPLE
    .\Get-JobStatus.ps1 -PassThru | Where-Object Status -eq 'CIRCUIT_OPEN'
    현재 서킷 브레이커가 열린 작업만 필터링하여 조회합니다.
#>
[CmdletBinding()]
param(
    [Parameter(HelpMessage = '스케줄러 기본 디렉터리 경로')]
    [string]$BaseDir = $PSScriptRoot,

    [Parameter(HelpMessage = '결과 객체를 파이프라인으로 반환')]
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BaseDir) {
    $BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$JobsDir = Join-Path $BaseDir 'jobs'
$StateDir = Join-Path $BaseDir 'state'

if (-not (Test-Path -LiteralPath $JobsDir)) {
    Write-Host 'jobs directory not found.' -ForegroundColor Red
    return
}

function Get-JobKey {
    param([string]$JobFullPath)
    $normalizedPath = $JobFullPath.ToLowerInvariant()
    $bytes = [Text.Encoding]::UTF8.GetBytes($normalizedPath)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
        return ([BitConverter]::ToString($hash) -replace '-', '').ToLower()
    }
    finally { $sha.Dispose() }
}

function Get-JobState {
    param([string]$JobFullPath)
    $key = Get-JobKey $JobFullPath
    $p = Join-Path $StateDir ("job_" + $key + ".json")
    if (Test-Path -LiteralPath $p) {
        try {
            return Get-Content -Raw -Path $p | ConvertFrom-Json
        }
        catch { return $null }
    }
    return $null
}

function Test-ProcessAlive {
    param([int]$ProcessId, [string]$StartTime)
    if ($ProcessId -le 0) { return $false }
    try {
        $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($null -eq $proc -or $proc.HasExited) { return $false }

        if ($StartTime) {
            $actualStart = $proc.StartTime.ToString('yyyy-MM-dd HH:mm:ss.fff')
            return ($actualStart -eq $StartTime)
        }
        return $true
    }
    catch { return $false }
}

function Get-JobDef {
    param([string]$BaseNameNoExt)
    $tokens = @($BaseNameNoExt -split '\s+' | Where-Object { $_ -ne '' })
    if ($tokens.Count -lt 6) { return $null }
    $concurrent = $false
    $i = $tokens.Count - 1
    while ($i -ge 0 -and ($tokens[$i] -match '^(TIMEOUT|CB|NOW|LOG|CONCURRENT)=(.+)$')) {
        $key = $Matches[1].ToUpper()
        $val = $Matches[2].ToUpper()
        if ($key -eq 'CONCURRENT') { $concurrent = ($val -eq '1' -or $val -eq 'TRUE') }
        $i--
    }
    return [pscustomobject]@{ Concurrent = $concurrent }
}

$jobFiles = @(Get-ChildItem -Path $JobsDir -Filter '*.ps1' -File -ErrorAction SilentlyContinue)
$results = @()

foreach ($jf in $jobFiles) {
    $state = Get-JobState -JobFullPath $jf.FullName
    $jobDef = Get-JobDef -BaseNameNoExt $jf.BaseName

    $key = Get-JobKey $jf.FullName
    $lockPath = Join-Path $StateDir ("job_" + $key + ".lock")
    $isRunning = Test-Path -LiteralPath $lockPath

    $status = 'IDLE'
    if ($isRunning) {
        $status = 'RUNNING'
        if ($state -and $state.PSObject.Properties.Name -contains 'PendingPid' -and $state.PendingPid) {
            $pendingStart = if ($state.PSObject.Properties.Name -contains 'PendingStartTime') { [string]$state.PendingStartTime } else { '' }
            if (-not (Test-ProcessAlive -ProcessId ([int]$state.PendingPid) -StartTime $pendingStart)) {
                $status = 'RUNNING(STALE?)'
            }
        }
    }

    if ($state -and $state.PSObject.Properties.Name -contains 'CircuitOpenUntil' -and $state.CircuitOpenUntil) {
        if ([datetime]$state.CircuitOpenUntil -gt (Get-Date)) {
            $status = 'CIRCUIT_OPEN'
        }
    }

    $lastRun = if ($state -and $state.PSObject.Properties.Name -contains 'LastRun' -and $state.LastRun) { [datetime]$state.LastRun } else { $null }
    $nextRun = if ($state -and $state.PSObject.Properties.Name -contains 'NextRun' -and $state.NextRun) { [datetime]$state.NextRun } else { $null }
    $lastExit = if ($state -and $state.PSObject.Properties.Name -contains 'LastExitCode' -and $null -ne $state.LastExitCode) { $state.LastExitCode } else { '-' }
    $fails = if ($state -and $state.PSObject.Properties.Name -contains 'ConsecutiveFailures' -and $state.ConsecutiveFailures) { $state.ConsecutiveFailures } else { 0 }
    $isConcurrent = if ($jobDef -and $jobDef.Concurrent) { 'Y' } else { 'N' }

    $results += [pscustomobject]@{
        JobName = $jf.BaseName
        Status  = $status
        Conc    = $isConcurrent
        LastRun = if ($lastRun) { $lastRun.ToString('yyyy-MM-dd HH:mm:ss') } else { '-' }
        NextRun = if ($nextRun) { $nextRun.ToString('yyyy-MM-dd HH:mm:ss') } else { '-' }
        Exit    = $lastExit
        Fails   = $fails
    }
}

$sortedResults = @($results | Sort-Object -Property JobName)

Write-Host ''
Write-Host '=== Job Status Dashboard ===' -ForegroundColor Cyan
if ($sortedResults.Count -eq 0) {
    Write-Host 'No jobs found.'
}
else {
    $tableText = $sortedResults | Format-Table -AutoSize | Out-String -Width 4096
    Write-Host $tableText
}
Write-Host "Current Time: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"

if ($PassThru) {
    Write-Output $sortedResults
}
