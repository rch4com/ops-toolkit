# Remove-ExpiredIISCertificate

> Windows Server(IIS)의 로컬 컴퓨터 인증서 저장소(`My`, `WebHosting`)에서 **만료되었고 어디에도 바인딩되지 않은 인증서만 안전하게 식별 및 삭제**하는 PowerShell 도구입니다. (PowerShell 5.1 & 7+ 겸용)

---

## 1. 제작 취지 (Why this tool?)

웹 서버(IIS)를 장기간 운영하다 보면 인증서 갱신 이력이 쌓이면서 만료된 인증서가 수십~수백 개씩 방치되는 경우가 많습니다.
하지만 관리자가 수동으로 인증서를 지우려 할 때 다음과 같은 위험이 따릅니다:
- **장애 위험**: 만료되었지만 아직 IIS 사이트나 `HTTP.sys` 포트에 바인딩되어 사용 중인 인증서를 실수로 삭제하면 웹 서비스 바인딩 오류가 발생할 수 있습니다.
- **식별의 어려움**: `certlm.msc` 콘솔에서는 어떤 인증서가 현재 IIS에 바인딩되어 있는지 한눈에 교차 확인하기 어렵습니다.

이 도구는 **IIS(`Microsoft.Web.Administration`) 및 `HTTP.sys`(`netsh http show sslcert`) 바인딩 목록을 전수 대조**하여, 현재 사용 중인 인증서는 철저히 보호하고 **'만료 + 미바인딩' 인증서만 안전하게 정리**하며, 모든 점검/삭제 내역을 현재 실행 경로 하위의 CSV 파일로 남깁니다.

---

## 2. 실행 환경 및 사전 요구사항

- **OS**: Windows Server 2016 / 2019 / 2022 / 2025 또는 Windows 10 / 11 (IIS 설치 환경)
- **PowerShell**: Windows PowerShell `5.1` 또는 PowerShell `7.0+` (`pwsh`) — **64비트(x64) 프로세스 필수**
- **권한**: **로컬 관리자 권한(Run as Administrator)** 필수 (`LocalMachine` 인증서 저장소 및 `HTTP.sys` 조회/수정)
- **IIS 구성 요소**: `%windir%\System32\inetsrv\Microsoft.Web.Administration.dll` 필요 (없을 경우 `-InstallMissingIISComponents` 옵션으로 설치 가능)

---

## 3. 사용법 및 옵션 (Usage & Options)

| 파라미터 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :--- |
| `-List` | 선택* | `false` | 인증서 상태 분류 및 콘솔/CSV 리포트만 생성 (삭제 안 함) |
| `-Execute` | 선택* | `false` | 만료 + 미바인딩(`DELETE_CANDIDATE`) 인증서를 실제로 삭제 |
| `-WhatIf` | 선택 | `false` | `-Execute -WhatIf` 형태로 실행 시 실제 삭제 없이 삭제 대상만 시뮬레이션 |
| `-Confirm` | 선택 | `false` | `-Execute -Confirm` 실행 시 각 인증서 삭제 전 사용자 확인 프롬프트 표시 |
| `-LogDirectory` | 선택 | `.\Logs` | CSV 결과 파일을 저장할 폴더 경로 (기본값: **현재 실행 경로 하위의 `Logs` 폴더**) |
| `-InstallMissingIISComponents` | 선택 | `false` | `Microsoft.Web.Administration.dll` 누락 시 Windows Server 기능 자동 설치 시도 |
| `-PassThru` | 선택 | `false` | 점검/처리 결과(`PSCustomObject[]`)를 파이프라인으로 반환 |
| `-Help` | 선택 | `false` | 콘솔 사용법 요약 출력 |

> `*` `-List`, `-Execute`, `-Help` 중 하나를 지정해야 합니다 (`-List`와 `-Execute`는 동시 사용 불가).

---

## 4. 주요 안전장치 (Safety Mechanisms)

1. **바인딩 이중 검증**: IIS 사이트 HTTPS 바인딩과 `netsh.exe http show sslcert`의 SHA-1(40자)/SHA-256(64자) 해시를 모두 수집하여 보호 목록으로 관리합니다.
2. **CCS(중앙 인증서 저장소) 보호**: IIS 사이트 중 하나라도 CCS(Centralized Certificate Store) 바인딩을 사용 중이면 예기치 않은 영향을 막기 위해 즉시 작업을 중단합니다.
3. **만료 + 바인딩 인증서 경고 표시**: 이미 만료되었으나 여전히 바인딩되어 있는 인증서는 삭제하지 않고 콘솔에 **빨간색 경고**로 표시하며, CSV 파일의 `Status` 컬럼에 `**만료 인증서 (바인딩 사용 중)**`으로 강조 기록합니다.
4. **삭제 직전 재검증 (TOCTOU 방지)**: `-Execute` 실행 시 각 인증서를 지우기 직전에 바인딩 여부와 만료 여부를 다시 확인합니다.
5. **Fail-Stop 정책**: 삭제 도중 단 1건이라도 오류가 발생하면 즉시 루프를 중단하고 나머지 후보 항목은 `삭제 미처리(NOT_PROCESSED)`로 남겨 추가 피해를 방지합니다.

---

## 5. 실행 예제 (Examples)

### 1) 인증서 상태 조회 및 CSV 리포트 생성 (안전 조회)
```powershell
.\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1 -List
```
- 현재 실행 폴더 하위의 `.\Logs\CertCleanup_yyyyMMdd_HHmmss.csv`에 결과가 저장됩니다.

### 2) 삭제 시뮬레이션 (`-WhatIf`)
```powershell
.\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1 -Execute -WhatIf
```
- 실제로 인증서를 삭제하지 않고 어떤 인증서와 기능이 처리될지 시뮬레이션합니다.

### 3) 만료된 미바인딩 인증서 실제 삭제
```powershell
.\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1 -Execute
```

### 4) 파이프라인으로 결과 객체 받아서 후속 처리 (`-PassThru`)
```powershell
$certs = .\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1 -List -PassThru
$certs | Where-Object StatusCode -eq 'EXPIRED_BOUND'
```
