#Requires -Version 5.1
<#
.SYNOPSIS
    작업 파일명의 Cron-Safe 문법을 파싱하고 다음 실행 예정 시각(NextRun)을 사전 검증합니다.

.DESCRIPTION
    새로 작성한 작업 파일명이나 jobs/ 폴더에 있는 .ps1 파일들이 올바른 Cron-Safe 규칙을 따르는지 검사하고,
    기준 시각(-Now)으로부터 가장 빠른 다음 실행 예정 시각(NextRun)과 적용된 옵션(TIMEOUT, CB, NOW, LOG, CONCURRENT)을 출력합니다.

.PARAMETER FileNames
    직접 검증할 작업 파일명(확장자 제외 또는 포함) 목록입니다. 생략 시 jobs/ 폴더의 .ps1 파일들을 자동으로 검사합니다.

.PARAMETER BaseDir
    스케줄러 기본 디렉터리 경로입니다 (기본값: 스크립트 위치).

.PARAMETER Now
    다음 실행 시각 계산의 기준이 되는 시각입니다 (기본값: 현재 시각).

.PARAMETER PassThru
    검증 결과 객체(PSCustomObject[])를 파이프라인으로 반환합니다.

.EXAMPLE
    .\Test-CronSchedule.ps1
    jobs/ 폴더 내 모든 스크립트의 Cron-Safe 문법과 다음 실행 시각을 확인합니다.

.EXAMPLE
    .\Test-CronSchedule.ps1 -FileNames "backup_db 30 2 L X X TIMEOUT=1800 CB=3" -PassThru
    지정한 파일명 패턴을 파싱하고 결과 객체를 반환합니다.
#>
[CmdletBinding()]
param(
    [Parameter(HelpMessage = '검증할 파일명 목록 (생략 시 jobs 폴더 스캔)')]
    [string[]]$FileNames,

    [Parameter(HelpMessage = '스케줄러 기본 디렉터리 경로')]
    [string]$BaseDir = $PSScriptRoot,

    [Parameter(HelpMessage = '계산 기준 시각')]
    [datetime]$Now = (Get-Date),

    [Parameter(HelpMessage = '결과 객체를 파이프라인으로 반환')]
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BaseDir) {
    $BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}

function ConvertFrom-JobFileName {
    param([string]$BaseNameNoExt)

    $cleanName = $BaseNameNoExt -replace '\.ps1$', ''
    $tokens = @($cleanName -split '\s+' | Where-Object { $_ -ne '' })
    if ($tokens.Count -lt 6) { return $null }

    $timeout = 0
    $cbN = 5
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

function Get-NextCronTime {
    param([datetime]$NowTime, $Cron)
    $t = (Get-Date $NowTime).AddSeconds(-$NowTime.Second).AddMilliseconds(-$NowTime.Millisecond).AddMinutes(1)

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

if (-not $FileNames -or $FileNames.Count -eq 0) {
    $jobsDir = Join-Path $BaseDir 'jobs'
    if (Test-Path -LiteralPath $jobsDir) {
        $FileNames = @(Get-ChildItem -Path $jobsDir -Filter '*.ps1' -File | Select-Object -ExpandProperty BaseName)
    }
    else {
        $FileNames = @(
            'daily_4am 0 4 X X X',
            'job_1min X@1 X X X X',
            'job_5min X@5 X X X X',
            'month_end 30 18 L X X TIMEOUT=600 CB=3',
            'weekday_9am 0 9 X X MON-FRI',
            'year_end 55 23 31 12 X'
        )
    }
}

$results = @()

foreach ($name in $FileNames) {
    $def = ConvertFrom-JobFileName -BaseNameNoExt $name
    if ($null -eq $def) {
        Write-Host "[FAIL] 파싱 실패: $name" -ForegroundColor Red
        $results += [pscustomobject]@{
            FileName   = $name
            Valid      = $false
            Prefix     = $null
            CronExpr   = $null
            NextRun    = $null
            TimeoutSec = 0
            CircuitN   = 0
            NowOpt     = $false
            LogOpt     = $false
            Concurrent = $false
        }
        continue
    }

    $nextRun = Get-NextCronTime -NowTime $Now -Cron $def.Cron
    $cronExpr = "$($def.Cron.Min) $($def.Cron.Hour) $($def.Cron.Dom) $($def.Cron.Mon) $($def.Cron.Dow)"
    $valid = ($null -ne $nextRun)

    $results += [pscustomobject]@{
        FileName   = $name
        Valid      = $valid
        Prefix     = $def.Prefix
        CronExpr   = $cronExpr
        NextRun    = $nextRun
        TimeoutSec = $def.TimeoutSec
        CircuitN   = $def.CircuitN
        NowOpt     = $def.NowOpt
        LogOpt     = $def.LogOpt
        Concurrent = $def.Concurrent
    }
}

Write-Host ''
Write-Host "=== Cron-Safe Schedule Validation (Base: $($Now.ToString('yyyy-MM-dd HH:mm:ss'))) ===" -ForegroundColor Cyan
$tableText = $results |
    Select-Object Prefix, CronExpr, NextRun, TimeoutSec, CircuitN, NowOpt, LogOpt, Concurrent, Valid |
    Format-Table -AutoSize |
    Out-String -Width 4096
Write-Host $tableText

if ($PassThru) {
    Write-Output $results
}
