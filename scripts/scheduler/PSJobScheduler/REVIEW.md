# REVIEW.md — PSJobScheduler

## 1. 품질 및 안전성 체크리스트

- [x] **보안 및 사내 고유 명칭 제거**:
  - 비밀번호, API 키, 사내 IP 하드코딩이 없는가?
  - 기존 `Feelanet-` 접두사가 모두 범용 명칭(`PSJobScheduler`, `PSJobScheduler-Logs`)으로 교체되었는가?
- [x] **안전성 (`-WhatIf` & Graceful Shutdown)**:
  - `Install-PSSchedule-NSSM.ps1 -WhatIf` 및 `Uninstall-PSSchedule-NSSM.ps1 -WhatIf` 실행 시 실제 서비스 등록/제거 없이 안전하게 시뮬레이션되는가?
  - `schedule.ps1 -RunOnce -WhatIf` 실행 시 하위 프로세스를 실제로 실행하지 않고 시뮬레이션되는가?
  - `nssm.exe`가 폴더 내에 포함되어 서비스 등록 요건을 충족하는가?
- [x] **사용성 및 샘플 구성**:
  - `jobs/` 폴더의 6개 샘플 스크립트와 `Test-CronSchedule.ps1`, `Get-JobStatus.ps1`가 `README.md`에 상세히 안내되어 있는가?
  - 불필요한 백업/디버그 파일(`debug_json.ps1`, `schedule-bak.stop`, `.agents/`)이 제외되었는가?
- [x] **테스트 및 문서화**:
  - 필수 6대 문서(`README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`)가 모두 작성되었는가?
  - `tests/powershell/PSJobScheduler.Tests.ps1` 단위 테스트가 모두 통과했는가?
  - 루트 `README.md` 카탈로그에 등록되었는가?

---

## 2. 리뷰 기록

### [2026-11-12] ScriptJobs → ops-toolkit (`scripts/scheduler/PSJobScheduler`) 통합 리뷰
- **상태**: PASS
- **주요 확인 사항**:
  - `Feelanet-PSJobScheduler` → `PSJobScheduler` 변경 완료
  - `debug_cron-bak.ps1`을 최신 파서 기반 `Test-CronSchedule.ps1`로 업그레이드하여 포함
  - `Introduction_and_UseCases.md`, `schedule_readme.md`, `Service_readme.md`의 핵심 내용을 `README.md`와 `SPEC.md`에 통합 완료
  - Pester 단위 테스트 통과 확인 완료
