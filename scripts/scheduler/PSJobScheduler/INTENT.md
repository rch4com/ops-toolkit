# INTENT.md — PSJobScheduler

## 1. 해결하려는 문제 (Problem Statement)
- Windows 환경에서 다수의 운영/배치 PowerShell 스크립트(`.ps1`)를 주기적으로 실행할 때, Windows 기본 작업 스케줄러(Task Scheduler)는 GUI/XML 중심이라 스케줄 설정을 파일 단위로 직관적으로 파악하거나 Git으로 형상 관리하기 어렵습니다.
- 개별 스크립트마다 타임아웃 강제 종료(`TIMEOUT`), 연속 실패 시 실행 차단(`CB` 서킷 브레이커), 실패 시 단계별 재시도(Backoff), 일자별 UTF-8 로그 분리 및 Windows 서비스 등록(NSSM)을 매번 구현하기 번거롭습니다.
- 기존 개별 저장소(`ScriptJobs`)로 관리되던 스케줄러를 `ops-toolkit` 모노레포로 통합하면서, 특정 사내 명칭(`Feelanet-`)을 범용 명칭(`PSJobScheduler`)으로 표준화하고 `-WhatIf` 안전장치와 문서/테스트 체계를 갖춰 누구나 쉽게 재사용할 수 있게 해야 합니다.

---

## 2. 기대 결과 및 성공 기준 (Expected Outcomes)
- **파일명 기반 Cron-Safe 스케줄링**: `jobs/` 폴더에 `<이름> <분> <시> <일> <월> <요일> [옵션].ps1` 파일을 넣는 것만으로 스케줄링과 보호 정책(`TIMEOUT`, `CB`, `NOW`, `LOG`, `CONCURRENT`)이 자동 적용됩니다.
- **범용 명칭 및 파라미터화**: 서비스명(`PSJobScheduler`), 이벤트 로그명(`PSJobScheduler-Logs`), 이벤트 소스(`PSJobScheduler`) 등을 범용 기본값으로 설정하고 파라미터로 주입할 수 있습니다.
- **안전장치(`-WhatIf` / `-RunOnce`) 보강**:
  - `Install-PSSchedule-NSSM.ps1` 및 `Uninstall-PSSchedule-NSSM.ps1`에서 `-WhatIf` 실행 시 실제 서비스나 이벤트 로그를 변경하지 않고 시뮬레이션합니다.
  - `schedule.ps1`에서 `-WhatIf` 및 `-RunOnce`를 지원하여 실제 하위 작업 프로세스를 띄우지 않고도 스케줄 트리거 여부를 안전하게 점검할 수 있습니다.
- **사용자 친화적 검증 도구 및 샘플 제공**:
  - `jobs/` 폴더의 6종 샘플 스크립트와 `Test-CronSchedule.ps1`(Cron 문법/다음 실행 시각 검증기), `Get-JobStatus.ps1`(상태 대시보드), `nssm.exe`(서비스 매니저)를 기본 포함하여 다운로드 즉시 검증 및 서비스 등록이 가능합니다.

---

## 3. Non-Goals (하지 않을 것 / 제약 사항)
- **외부 DBMS 또는 무거운 에이전트 의존성 도입 금지**: 순수 PowerShell과 로컬 파일 시스템(`jobs/`, `state/`, `logs/`), 동봉된 `nssm.exe`만으로 동작하는 경량 아키텍처를 유지합니다.
- **실행 중인 작업의 강제 중단 지양**: 서비스 중지나 스케줄러 종료 시(타임아웃 초과 제외) `schedule.stop`을 통해 실행 중인 작업이 안전하게 끝날 때까지 기다리는 Graceful Shutdown 원칙을 지킵니다.
