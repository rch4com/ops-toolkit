#Requires -Version 5.1
<#
.SYNOPSIS
    IIS 및 HTTP.sys 바인딩을 확인하여 만료된 미바인딩 로컬 컴퓨터 인증서를 안전하게 조회 및 정리합니다.

.DESCRIPTION
    Windows Server(IIS)의 로컬 컴퓨터 인증서 저장소(Cert:\LocalMachine\My, Cert:\LocalMachine\WebHosting)를 점검하여
    1) 유효한 인증서
    2) 만료되었고 어디에도 바인딩되지 않은 인증서 (삭제 대상)
    3) 만료되었으나 IIS 또는 HTTP.sys에 바인딩되어 사용 중인 인증서 (주의/경고)
    그룹으로 분류하고, 현재 실행 폴더 하위의 Logs 디렉터리에 CSV 리포트를 생성합니다.
    -Execute 옵션 사용 시 만료된 미바인딩 인증서만 안전하게 삭제하며, -WhatIf 시뮬레이션을 지원합니다.

.PARAMETER List
    인증서 조회 및 CSV 저장만 수행합니다 (인증서 삭제 없음).

.PARAMETER Execute
    만료된 미바인딩 인증서를 실제로 삭제합니다 (-WhatIf 및 -Confirm 지원).

.PARAMETER Help
    콘솔에 사용 방법을 출력하고 종료합니다.

.PARAMETER InstallMissingIISComponents
    Microsoft.Web.Administration.dll이 없을 경우 관련 Windows 기능(Web-Mgmt-Tools, Web-Scripting-Tools) 설치를 시도합니다.

.PARAMETER LogDirectory
    CSV 결과 파일이 저장될 폴더 경로입니다. 기본값은 현재 실행 경로($PWD) 하위의 'Logs' 폴더입니다.

.PARAMETER PassThru
    점검 및 처리 완료된 인증서 결과 객체(PSCustomObject[])를 파이프라인으로 반환합니다.

.EXAMPLE
    .\Remove-ExpiredIISCertificate.ps1 -List
    인증서 상태를 조회하고 현재 폴더 하위 .\Logs\CertCleanup_<타임스탬프>.csv 파일을 생성합니다.

.EXAMPLE
    .\Remove-ExpiredIISCertificate.ps1 -Execute -WhatIf
    실제로 인증서를 삭제하지 않고 어떤 만료 미바인딩 인증서가 삭제될지 시뮬레이션합니다.

.EXAMPLE
    .\Remove-ExpiredIISCertificate.ps1 -Execute
    만료된 미바인딩 인증서를 실제로 삭제하고 결과를 CSV로 저장합니다.

.EXAMPLE
    .\Remove-ExpiredIISCertificate.ps1 -List -PassThru | Where-Object StatusCode -eq 'EXPIRED_BOUND'
    조회 결과를 파이프라인으로 받아 만료되었으나 바인딩 중인 위험 인증서만 필터링합니다.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(HelpMessage = '인증서 조회 및 CSV 저장 (삭제 없음)')]
    [switch]$List,

    [Parameter(HelpMessage = '만료된 미바인딩 인증서 실제 삭제')]
    [switch]$Execute,

    [Parameter(HelpMessage = '사용 방법 출력')]
    [switch]$Help,

    [Parameter(HelpMessage = 'IIS 관리 DLL이 없으면 관련 기능 설치 시도')]
    [switch]$InstallMissingIISComponents,

    [Parameter(HelpMessage = 'CSV 로그 저장 폴더 경로 (기본값: 현재 실행 폴더 하위 Logs)')]
    [ValidateNotNullOrEmpty()]
    [string]$LogDirectory = (Join-Path -Path (Get-Location).Path -ChildPath 'Logs'),

    [Parameter(HelpMessage = '결과 객체를 파이프라인으로 반환')]
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ============================================================
# 1. 사용 방법
# ============================================================

function Show-Usage {
    $scriptName = Split-Path -Leaf $PSCommandPath

    Write-Host ''
    Write-Host '============================================================' -ForegroundColor Cyan
    Write-Host ' IIS 만료 인증서 관리 도구 (PowerShell 5.1 & 7+ 겸용)' -ForegroundColor Cyan
    Write-Host '============================================================' -ForegroundColor Cyan

    Write-Host ''
    Write-Host '[사용 방법]' -ForegroundColor Yellow
    Write-Host "  .\$scriptName -List"
    Write-Host "  .\$scriptName -Execute -WhatIf"
    Write-Host "  .\$scriptName -Execute"
    Write-Host "  .\$scriptName -List -InstallMissingIISComponents"
    Write-Host "  .\$scriptName -Execute -InstallMissingIISComponents"
    Write-Host "  .\$scriptName -List -LogDirectory .\Logs\Certs"
    Write-Host "  .\$scriptName -List -PassThru"
    Write-Host "  .\$scriptName -Help"

    Write-Host ''
    Write-Host '[파라미터 설명]' -ForegroundColor Yellow
    Write-Host '  -List'
    Write-Host '      인증서 조회 및 CSV 저장 (삭제 없음)'
    Write-Host '  -Execute'
    Write-Host '      만료된 미바인딩 인증서 실제 삭제 (-WhatIf 시뮬레이션 지원)'
    Write-Host '  -InstallMissingIISComponents'
    Write-Host '      IIS 관리 DLL이 없으면 관련 기능 설치 시도'
    Write-Host '  -LogDirectory <경로>'
    Write-Host '      CSV 로그 저장 위치 변경 (기본값: 현재 실행 폴더 하위 .\Logs)'
    Write-Host '  -PassThru'
    Write-Host '      점검 결과 객체(PSCustomObject)를 파이프라인으로 반환'
    Write-Host '  -Help'
    Write-Host '      사용 방법 출력'

    Write-Host ''
    Write-Host '[주의 사항]' -ForegroundColor Red
    Write-Host '  1. 실제 작업은 관리자 권한이 필요합니다.'
    Write-Host '  2. 64비트(x64) PowerShell 5.1 또는 PowerShell 7+ 환경에서 실행하세요.'
    Write-Host '  3. -Execute는 인증서를 실제로 삭제합니다 (사전 -WhatIf 확인 권장).'
    Write-Host '  4. 바인딩된 만료 인증서는 삭제하지 않습니다.'
    Write-Host '  5. IIS 외 다른 서비스 사용 여부는 별도 확인해야 합니다.'
    Write-Host ''
}

# ============================================================
# 2. 필수 파라미터 검증
# ============================================================

if ($Help) {
    Show-Usage
    return
}

if (-not $List -and -not $Execute) {
    Show-Usage
    Write-Host '[안내] -List 또는 -Execute 파라미터가 필요합니다.' -ForegroundColor Yellow
    return
}

if ($List -and $Execute) {
    Show-Usage
    Write-Host '[오류] -List와 -Execute는 동시에 사용할 수 없습니다.' -ForegroundColor Red
    return
}

# ============================================================
# 3. 관리자 및 실행 환경 확인
# ============================================================

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '관리자 권한으로 PowerShell을 실행하세요.'
}

if (-not [Environment]::Is64BitProcess) {
    throw '64비트 PowerShell에서 실행하세요.'
}

# ============================================================
# 4. 한글 상태 정의
# ============================================================

$StatusNames = @{
    KEEP_VALID          = '유효 인증서'
    KEEP_BOUND          = '바인딩 사용 중'
    EXPIRED_BOUND       = '만료 인증서 (바인딩 사용 중)'
    DELETE_CANDIDATE    = '만료 인증서 (삭제 대상)'
    SKIPPED_NOW_BOUND   = '삭제 제외 (바인딩 확인)'
    SKIPPED_NOT_FOUND   = '삭제 제외 (인증서 없음)'
    SKIPPED_NOT_EXPIRED = '삭제 제외 (유효기간 확인)'
    DELETED             = '삭제 완료'
    ERROR               = '삭제 오류'
    NOT_PROCESSED       = '삭제 미처리'
}

function Set-CertStatus {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Item,

        [Parameter(Mandatory = $true)]
        [string]$Code
    )

    $Item.StatusCode = $Code
    $Item.Status = $StatusNames[$Code]
}

# ============================================================
# 5. IIS 관리 DLL 확인 및 선택적 설치
# ============================================================

function Initialize-IISAdministration {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    $dllPath = Join-Path -Path $env:windir -ChildPath 'System32\inetsrv\Microsoft.Web.Administration.dll'

    Write-Host '[CHECK] IIS 관리 DLL 확인'

    if (-not (Test-Path -LiteralPath $dllPath)) {
        Write-Warning "DLL 없음: $dllPath"

        if (-not $InstallMissingIISComponents) {
            throw 'DLL이 없습니다. -InstallMissingIISComponents 옵션을 사용하세요.'
        }

        Write-Host '[INSTALL] IIS 관리 기능 확인'

        Import-Module -Name ServerManager -ErrorAction Stop

        $featureNames = @(
            'Web-Mgmt-Tools',
            'Web-Scripting-Tools'
        )

        $missing = @()

        foreach ($name in $featureNames) {
            $feature = Get-WindowsFeature -Name $name -ErrorAction Stop

            if ($null -eq $feature) {
                throw "기능 확인 실패: $name"
            }

            if (-not $feature.Installed) {
                $missing += $name
            }
        }

        if ($missing.Count -gt 0) {
            $missingText = $missing -join ', '
            if ($PSCmdlet.ShouldProcess($missingText, 'Install-WindowsFeature')) {
                Write-Host "[INSTALL] $missingText"

                $result = Install-WindowsFeature -Name $missing -ErrorAction Stop

                if (-not $result.Success) {
                    throw 'IIS 관리 기능 설치 실패'
                }

                if ($result.RestartNeeded -eq 'Yes') {
                    throw '서버 재시작 후 다시 실행하세요.'
                }
            }
        }
    }

    if (-not (Test-Path -LiteralPath $dllPath)) {
        throw "IIS 관리 DLL이 없습니다: $dllPath"
    }

    if (-not ('Microsoft.Web.Administration.ServerManager' -as [type])) {
        Add-Type -Path $dllPath -ErrorAction Stop
    }

    Write-Host '[OK] IIS 관리 DLL 로드 성공' -ForegroundColor Green
}

# ============================================================
# 6. IIS / HTTP.sys 바인딩 인증서 조회
# ============================================================

function Get-ProtectedThumbprints {
    $protected = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase
    )

    $manager = New-Object Microsoft.Web.Administration.ServerManager

    try {
        foreach ($site in $manager.Sites) {
            foreach ($binding in $site.Bindings) {
                if ($binding.Protocol -ne 'https') {
                    continue
                }

                # CCS 바인딩이 있으면 안전을 위해 중단
                if (([int]$binding.SslFlags -band 2) -ne 0) {
                    throw "CCS 바인딩 발견: $($site.Name)"
                }

                $hash = $binding.CertificateHash

                if ($null -eq $hash -or $hash.Length -eq 0) {
                    throw "인증서 해시 없음: $($site.Name)"
                }

                $thumb = [BitConverter]::ToString($hash).Replace('-', '').ToUpperInvariant()
                [void]$protected.Add($thumb)
            }
        }
    }
    finally {
        $manager.Dispose()
    }

    # HTTP.sys SSL 바인딩 확인
    $netshOutput = & netsh.exe http show sslcert 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw 'HTTP.sys SSL 바인딩 조회 실패'
    }

    $raw = $netshOutput -join "`n"

    # 인증서 해시 추출 (SHA-1 40자 또는 SHA-256 64자)
    $pattern = '(?i)(?<![0-9a-f])(?:[0-9a-f]{64}|[0-9a-f]{40})(?![0-9a-f])'

    foreach ($match in [regex]::Matches($raw, $pattern)) {
        [void]$protected.Add($match.Value.ToUpperInvariant())
    }

    return ,$protected
}

# ============================================================
# 7. 인증서 그룹별 콘솔 출력
# ============================================================

function Show-CertificateGroup {
    param(
        [AllowEmptyCollection()]
        [object[]]$Certificates,
        [string]$Title,
        [ConsoleColor]$Color = 'Gray'
    )

    Write-Host ''
    Write-Host "========== $Title ==========" -ForegroundColor $Color

    if ($null -eq $Certificates -or $Certificates.Count -eq 0) {
        Write-Host '해당 인증서 없음' -ForegroundColor DarkGray
        return
    }

    $tableText = $Certificates |
        Select-Object `
            NotAfter,
            Expired,
            InUse,
            Status,
            Store,
            Subject,
            Thumbprint |
        Format-Table -AutoSize |
        Out-String -Width 4096

    Write-Host $tableText -ForegroundColor $Color
}

# ============================================================
# 8. 초기화 및 로그 설정 (현재 실행 폴더 하위 저장 지원)
# ============================================================

Initialize-IISAdministration

if (-not (Test-Path -LiteralPath $LogDirectory)) {
    New-Item -ItemType Directory -Path $LogDirectory -Force -WhatIf:$false -Confirm:$false | Out-Null
}

$resolvedLogDir = (Resolve-Path -LiteralPath $LogDirectory).Path
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logFile = Join-Path -Path $resolvedLogDir -ChildPath "CertCleanup_$timestamp.csv"

$protectedThumbs = Get-ProtectedThumbprints

$stores = @(
    'Cert:\LocalMachine\My',
    'Cert:\LocalMachine\WebHosting'
)

$now = Get-Date

$results = New-Object System.Collections.ArrayList
$candidates = New-Object System.Collections.ArrayList

# ============================================================
# 9. 인증서 조회 및 분류
# ============================================================

foreach ($store in $stores) {
    if (-not (Test-Path -LiteralPath $store)) {
        Write-Warning "저장소 없음: $store"
        continue
    }

    $certificates = @(
        Get-ChildItem -Path $store -ErrorAction Stop
    )

    foreach ($cert in $certificates) {
        $thumb = $cert.Thumbprint.ToUpperInvariant()

        $expired = ($cert.NotAfter -lt $now)
        $inUse = $protectedThumbs.Contains($thumb)

        # 정렬 기준
        # 0 = 유효 인증서
        # 1 = 만료 + 미바인딩 (삭제 대상)
        # 2 = 만료 + 바인딩 (빨간색 경고)
        $sortOrder = 0
        $statusCode = 'KEEP_VALID'

        if (-not $expired) {
            $sortOrder = 0
            if ($inUse) {
                $statusCode = 'KEEP_BOUND'
            }
        }
        elseif (-not $inUse) {
            $sortOrder = 1
            $statusCode = 'DELETE_CANDIDATE'
        }
        else {
            $sortOrder = 2
            $statusCode = 'EXPIRED_BOUND'
        }

        $item = [PSCustomObject][ordered]@{
            NotAfter   = $cert.NotAfter
            Expired    = $expired
            InUse      = $inUse
            Status     = $StatusNames[$statusCode]
            Store      = $store
            Subject    = $cert.Subject
            Thumbprint = $thumb
            StatusCode = $statusCode
            SortOrder  = $sortOrder
        }

        [void]$results.Add($item)

        if ($statusCode -eq 'DELETE_CANDIDATE') {
            [void]$candidates.Add($item)
        }
    }
}

# ============================================================
# 10. 실제 삭제 (-Execute, SupportsShouldProcess 연동)
# ============================================================

if ($Execute) {
    Write-Host ''
    if ($WhatIfPreference) {
        Write-Host '[WhatIf 시뮬레이션] 인증서 삭제 시뮬레이션 모드 활성화' -ForegroundColor Yellow
    }
    else {
        Write-Warning '인증서 삭제 모드 활성화'
    }

    for ($i = 0; $i -lt $candidates.Count; $i++) {
        $item = $candidates[$i]
        $path = Join-Path -Path $item.Store -ChildPath $item.Thumbprint

        try {
            # 삭제 직전 바인딩 재확인 (TOCTOU 방지)
            $currentProtected = Get-ProtectedThumbprints

            if ($currentProtected.Contains($item.Thumbprint)) {
                $item.InUse = $true
                $item.SortOrder = 2
                Set-CertStatus -Item $item -Code 'SKIPPED_NOW_BOUND'
                continue
            }

            if (-not (Test-Path -LiteralPath $path)) {
                Set-CertStatus -Item $item -Code 'SKIPPED_NOT_FOUND'
                continue
            }

            $currentCert = Get-Item -LiteralPath $path -ErrorAction Stop

            if ($currentCert.NotAfter -ge (Get-Date)) {
                $item.Expired = $false
                $item.SortOrder = 0
                Set-CertStatus -Item $item -Code 'SKIPPED_NOT_EXPIRED'
                continue
            }

            # SupportsShouldProcess (-WhatIf / -Confirm 지원)
            $actionDesc = "만료된 미바인딩 인증서 삭제 (Subject: $($item.Subject), NotAfter: $($item.NotAfter.ToString('yyyy-MM-dd')))"
            if ($PSCmdlet.ShouldProcess($path, $actionDesc)) {
                # 개인키 삭제 옵션은 사용하지 않음
                Remove-Item -LiteralPath $path -Confirm:$false -ErrorAction Stop

                Set-CertStatus -Item $item -Code 'DELETED'
                Write-Host "[삭제 완료] $($item.Thumbprint)" -ForegroundColor Green
            }
        }
        catch {
            Set-CertStatus -Item $item -Code 'ERROR'

            Write-Warning "삭제 실패: $($item.Thumbprint)"
            Write-Warning $_

            # 이후 항목은 삭제하지 않음
            for ($j = $i + 1; $j -lt $candidates.Count; $j++) {
                Set-CertStatus -Item $candidates[$j] -Code 'NOT_PROCESSED'
            }

            break
        }
    }
}

# ============================================================
# 11. 결과 정렬
# ============================================================

$sortedResults = @(
    $results |
        Sort-Object -Property SortOrder, NotAfter, Subject
)

$validCerts = @(
    $sortedResults |
        Where-Object { $_.SortOrder -eq 0 }
)

$deleteCerts = @(
    $sortedResults |
        Where-Object { $_.SortOrder -eq 1 }
)

$expiredBoundCerts = @(
    $sortedResults |
        Where-Object { $_.SortOrder -eq 2 }
)

# ============================================================
# 12. 콘솔 출력
# ============================================================

Write-Host ''
Write-Host '========== IIS 인증서 점검 결과 ==========' -ForegroundColor Cyan

# 1. 유효한 인증서
Show-CertificateGroup `
    -Certificates $validCerts `
    -Title '1. 유효한 인증서' `
    -Color Gray

# 2. 만료 인증서 (삭제 대상)
Show-CertificateGroup `
    -Certificates $deleteCerts `
    -Title '2. 만료 인증서 (삭제 대상)' `
    -Color Yellow

# 3. 만료 + 바인딩 인증서 (빨간색)
Show-CertificateGroup `
    -Certificates $expiredBoundCerts `
    -Title '3. 만료 인증서 (바인딩 사용 중 - 주의)' `
    -Color Red

# ============================================================
# 13. CSV 로그 저장 (PS 5.1 & PS 7+ 인코딩 호환)
# ============================================================

$csvResults = @(
    foreach ($item in $sortedResults) {
        $csvStatus = $item.Status

        if ($item.Expired -and $item.InUse) {
            $csvStatus = "**$csvStatus**"
        }

        [PSCustomObject][ordered]@{
            NotAfter   = $item.NotAfter
            Expired    = $item.Expired
            InUse      = $item.InUse
            Status     = $csvStatus
            Store      = $item.Store
            Subject    = $item.Subject
            Thumbprint = $item.Thumbprint
        }
    }
)

$csvEncoding = if ($PSVersionTable.PSVersion.Major -ge 6) { 'utf8BOM' } else { 'UTF8' }

$csvResults |
    Export-Csv `
        -LiteralPath $logFile `
        -NoTypeInformation `
        -Encoding $csvEncoding `
        -WhatIf:$false `
        -Confirm:$false

# ============================================================
# 14. 최종 결과 출력 및 파이프라인 반환
# ============================================================

$deletedCount = @(
    $results |
        Where-Object { $_.StatusCode -eq 'DELETED' }
).Count

Write-Host ''
Write-Host '========== 최종 결과 ==========' -ForegroundColor Cyan
Write-Host "전체 인증서 수     : $($results.Count)"
Write-Host "유효 인증서 수     : $($validCerts.Count)"
Write-Host "삭제 대상 그룹 수  : $($deleteCerts.Count)"
Write-Host "만료 + 바인딩 수   : $($expiredBoundCerts.Count)" -ForegroundColor Red
Write-Host "삭제 완료 수       : $deletedCount"
Write-Host "CSV 로그           : $logFile"

if ($List) {
    Write-Host ''
    Write-Host '[조회 전용] 인증서를 삭제하지 않았습니다.' -ForegroundColor Green
}
elseif ($Execute -and $WhatIfPreference) {
    Write-Host ''
    Write-Host '[WhatIf 완료] 시뮬레이션 모드이므로 실제 인증서를 삭제하지 않았습니다.' -ForegroundColor Yellow
}
elseif ($Execute) {
    Write-Host ''
    Write-Host '[완료] 인증서 삭제 작업이 종료되었습니다.' -ForegroundColor Green
}

if ($PassThru) {
    Write-Output $sortedResults
}
