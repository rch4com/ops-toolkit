# SPEC.md — Remove-ExpiredIISCertificate

## 1. 파일 및 실행 환경 사양
- **스크립트 경로**: `scripts/iis/Remove-ExpiredIISCertificate/Remove-ExpiredIISCertificate.ps1`
- **테스트 경로**: `tests/powershell/Remove-ExpiredIISCertificate.Tests.ps1`
- **호환 버전**: `#Requires -Version 5.1` (Windows PowerShell 5.1 및 PowerShell 7.x 겸용)
- **필수 실행 조건**: `-List` 또는 `-Execute` 실행 시 64비트 프로세스(`[Environment]::Is64BitProcess`) 및 로컬 관리자 권한 확인 (`-Help` 실행 시에는 권한 검사 전 즉시 도움말 출력)

---

## 2. 입력 파라미터 명세 (Input Contract)

```powershell
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(HelpMessage = '인증서 조회 및 CSV 저장 (삭제 없음)')]
    [switch]$List,

    [Parameter(HelpMessage = '만료된 미바인딩 인증서 실제 삭제 (-WhatIf 지원)')]
    [switch]$Execute,

    [Parameter(HelpMessage = '사용 방법 출력')]
    [switch]$Help,

    [Parameter(HelpMessage = 'IIS 관리 DLL이 없으면 관련 Windows 기능 설치 시도')]
    [switch]$InstallMissingIISComponents,

    [Parameter(HelpMessage = 'CSV 로그 저장 폴더 경로 (기본값: 현재 실행 폴더 하위 Logs)')]
    [ValidateNotNullOrEmpty()]
    [string]$LogDirectory = (Join-Path -Path (Get-Location).Path -ChildPath 'Logs'),

    [Parameter(HelpMessage = '점검/처리 결과 객체를 파이프라인으로 반환')]
    [switch]$PassThru
)
```

---

## 3. 출력 데이터 및 CSV 스키마 (Output Contract)

### 3.1 상태 코드 (`StatusCode` / `Status`)
| StatusCode | 한글 상태 (`Status`) | SortOrder | 설명 |
| :--- | :--- | :---: | :--- |
| `KEEP_VALID` | `유효 인증서` | `0` | 유효기간이 남아 있고 미바인딩된 인증서 |
| `KEEP_BOUND` | `바인딩 사용 중` | `0` | 유효기간이 남아 있고 IIS/HTTP.sys에 바인딩된 인증서 |
| `DELETE_CANDIDATE` | `만료 인증서 (삭제 대상)` | `1` | 만료되었고 바인딩되지 않은 인증서 (`-List` 또는 `-Execute -WhatIf` 시) |
| `DELETED` | `삭제 완료` | `1` | `-Execute`로 정상 삭제된 인증서 |
| `SKIPPED_NOW_BOUND` | `삭제 제외 (바인딩 확인)` | `2` | 삭제 직전 재조회 시 바인딩이 감지되어 보호된 인증서 |
| `SKIPPED_NOT_FOUND` | `삭제 제외 (인증서 없음)` | `1` | 삭제 직전 이미 제거되어 존재하지 않는 인증서 |
| `SKIPPED_NOT_EXPIRED` | `삭제 제외 (유효기간 확인)` | `0` | 삭제 직전 재확인 시 만료되지 않은 인증서 |
| `ERROR` | `삭제 오류` | `1` | 삭제 중 예외가 발생한 인증서 |
| `NOT_PROCESSED` | `삭제 미처리` | `1` | 이전 항목 삭제 오류로 인해 처리가 중단된 나머지 후보 인증서 |
| `EXPIRED_BOUND` | `만료 인증서 (바인딩 사용 중)` | `2` | 만료되었으나 현재 바인딩되어 사용 중인 위험 인증서 |

### 3.2 CSV 파일 저장 규칙
- **저장 경로**: `<LogDirectory>\CertCleanup_<yyyyMMdd_HHmmss>.csv`
  - 기본 `<LogDirectory>`는 `(Get-Location).Path` 하위의 `Logs` 폴더 (`.\Logs\CertCleanup_yyyyMMdd_HHmmss.csv`)
- **CSV 컬럼**: `NotAfter`, `Expired`, `InUse`, `Status`, `Store`, `Subject`, `Thumbprint`
  - `Expired -and InUse`인 항목은 CSV 가시성을 위해 `Status` 값을 `**<Status>**`로 감싸서 저장
- **인코딩**: PowerShell 5.1에서는 `UTF8` (UTF-8 with BOM), PowerShell 7+에서는 Excel 호환을 위해 `utf8BOM` 사용

### 3.3 파이프라인 반환 (`-PassThru`)
- `-PassThru` 스위치가 켜져 있을 때만 `$sortedResults`(`[PSCustomObject[]]`)를 표준 출력 스트림으로 반환합니다.

---

## 4. `-WhatIf` (`SupportsShouldProcess`) 동작 정의
- `-Execute -WhatIf`로 실행 시:
  - 인증서 목록 스캔, 바인딩 조회, 콘솔 그룹 출력 및 CSV 리포트 생성은 정상 수행하여 운영자가 결과를 확인할 수 있게 합니다.
  - `Remove-Item` 호출부를 `$PSCmdlet.ShouldProcess($path, "만료된 미바인딩 인증서 삭제 (Subject: $($item.Subject))")`로 감싸서 `-WhatIf` 시에는 실제 인증서 삭제가 일어나지 않고 상태가 `DELETE_CANDIDATE`로 유지됩니다.
  - `Install-WindowsFeature` 호출부도 `$PSCmdlet.ShouldProcess(...)`로 보호합니다.
