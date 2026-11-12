# PLAN.md — PSJobScheduler

- [x] **Step 1: 기존 `ScriptJobs` 저장소 분석 및 요구사항 인터뷰 완료**
  - 대상 경로: `scripts/scheduler/PSJobScheduler/`
  - 사내 고유 명칭(`Feelanet-`) 제거 및 범용 명칭(`PSJobScheduler`) 적용
  - `nssm.exe` 및 `jobs/*.ps1` 샘플 6종 포함 확정
  - `debug_cron-bak.ps1`을 사용자 검증 도구인 `Test-CronSchedule.ps1`로 개선/포함하고, 일회성 파일(`debug_json.ps1`, `schedule-bak.stop`, `.agents/`) 제외
- [x] **Step 2: 6대 필수 문서 생성**
  - `README.md` (소개, Cron-Safe 문법, 샘플 작업 설명, 실제 업무 활용 사례 통합)
  - `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md` 작성
- [x] **Step 3: 스케줄러 스크립트 세트 표준화 및 `-WhatIf` 보강**
  - `schedule.ps1`: 범용 이벤트로그명 적용, `[CmdletBinding(SupportsShouldProcess = $true)]`, `-RunOnce`, 파라미터화 적용
  - `schedule-service-host.ps1`: 파라미터화 및 Comment-Based Help 추가
  - `Install-PSSchedule-NSSM.ps1` & `Uninstall-PSSchedule-NSSM.ps1`: 범용 서비스명(`PSJobScheduler`), `SupportsShouldProcess`(`-WhatIf`) 안전장치 적용
  - `Get-JobStatus.ps1`: 한글 주석 정리, `-PassThru` 객체 반환 옵션 추가
  - `Test-CronSchedule.ps1`: 최신 옵션(`LOG`, `CONCURRENT` 포함) 파서 및 다음 실행 시각 검증 도구 구현
- [x] **Step 4: `.gitignore` 업데이트 및 Pester 단위 테스트 작성**
  - 스케줄러 런타임 폴더(`logs/`, `state/`, `schedule.stop`) `.gitignore` 반영
  - `tests/powershell/PSJobScheduler.Tests.ps1` 작성 및 검증
- [x] **Step 5: 루트 `README.md` 카탈로그 등록 및 기존 `ScriptJobs` 저장소 이전 안내 준비**
