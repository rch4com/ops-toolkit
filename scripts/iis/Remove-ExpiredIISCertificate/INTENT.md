# INTENT.md — Remove-ExpiredIISCertificate

## 1. 해결하려는 문제 (Problem Statement)
- Windows Server(IIS) 운영 시 만료된 인증서가 `Cert:\LocalMachine\My` 및 `Cert:\LocalMachine\WebHosting` 저장소에 누적되어 관리 가시성을 떨어뜨립니다.
- 수동 정리 시 현재 IIS 사이트나 `HTTP.sys`에 바인딩된 인증서를 실수로 삭제할 위험이 있으며, 반대로 이미 만료된 인증서가 운영 사이트에 바인딩된 채 방치되어 있는 문제를 조기에 발견하기 어렵습니다.
- 현장 서버마다 PowerShell 7이 설치되지 않은 경우(Windows PowerShell 5.1 기본 환경)도 있으므로 두 버전 모두에서 동일하게 동작해야 하며, 실행 결과 CSV 파일이 현재 작업 폴더 하위에 바로 생성되어야 운영자가 결과를 수집하기 편리합니다.

---

## 2. 기대 결과 및 성공 기준 (Expected Outcomes)
- **PowerShell 5.1 & 7+ 완벽 호환**: 두 런타임 모두에서 문법 오류나 인코딩 깨짐 없이 실행됩니다.
- **안전한 조회 및 삭제 (`-List` / `-Execute` + `-WhatIf`)**:
  - 기존 `-List`와 `-Execute` 인터페이스를 그대로 유지하면서, `[CmdletBinding(SupportsShouldProcess = $true)]`를 통해 `-Execute -WhatIf` 시뮬레이션과 `-Confirm`을 지원합니다.
- **현재 실행 경로 하위 CSV 저장**:
  - 별도 경로를 지정하지 않으면 현재 작업 디렉터리(`$PWD`) 하위의 `Logs\CertCleanup_yyyyMMdd_HHmmss.csv`에 결과를 저장합니다.
- **파이프라인 연동 (`-PassThru`)**:
  - 다른 스크립트나 명령어와 조합할 수 있도록 `-PassThru` 지정 시 구조화된 `PSCustomObject` 목록을 파이프라인으로 출력합니다.

---

## 3. Non-Goals (하지 않을 것 / 안전 제약)
- **바인딩된 인증서 자동 삭제 금지**: 만료되었더라도 IIS 또는 `HTTP.sys`에 바인딩된 인증서는 절대 삭제하지 않고 경고(`EXPIRED_BOUND`)로만 리포트합니다.
- **개인키(Private Key) 강제 영구 파기 금지**: 인증서 스토어 항목만 제거(`Remove-Item`)하며, 다른 서비스에 영향을 줄 수 있는 디스크 상의 원본 키 파일 강제 삭제는 수행하지 않습니다.
- **CCS(Centralized Certificate Store) 환경 임의 처리 금지**: CCS 바인딩이 감지되면 즉시 실행을 멈추고 운영자가 수동 확인하도록 합니다.
