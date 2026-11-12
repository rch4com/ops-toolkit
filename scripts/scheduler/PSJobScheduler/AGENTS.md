# AGENTS.md — PSJobScheduler

이 도구는 루트 [AGENTS.md](../../../AGENTS.md)의 규칙을 따르며, **Windows PowerShell 5.1 및 PowerShell 7+ (`pwsh`) 겸용**으로 동작하는 Cron-Safe 작업 스케줄러 패키지입니다.
코드를 수정하기 전에 현재 폴더의 [INTENT.md](./INTENT.md), [SPEC.md](./SPEC.md), [PLAN.md](./PLAN.md), [REVIEW.md](./REVIEW.md)를 반드시 확인하세요.

---

## 1. 로컬 검증 및 테스트 명령어

```powershell
# 1. Cron-Safe 파일명 파서 및 다음 실행 시각 검증
pwsh -NoProfile -File .\scripts\scheduler\PSJobScheduler\Test-CronSchedule.ps1

# 2. 작업 상태 대시보드 조회 (PassThru 객체 확인)
pwsh -NoProfile -File .\scripts\scheduler\PSJobScheduler\Get-JobStatus.ps1 -PassThru

# 3. 스케줄러 1회 루프 Dry-Run 시뮬레이션 (-RunOnce -WhatIf)
pwsh -NoProfile -File .\scripts\scheduler\PSJobScheduler\schedule.ps1 -RunOnce -WhatIf

# 4. 서비스 설치/제거 스크립트 Dry-Run 시뮬레이션 (-WhatIf)
pwsh -NoProfile -File .\scripts\scheduler\PSJobScheduler\Install-PSSchedule-NSSM.ps1 -WhatIf
pwsh -NoProfile -File .\scripts\scheduler\PSJobScheduler\Uninstall-PSSchedule-NSSM.ps1 -WhatIf

# 5. Pester 단위 테스트 실행
Invoke-Pester -Path .\tests\powershell\PSJobScheduler.Tests.ps1 -Output Detailed
```

---

## 2. 도구 고유 주의사항 (Critical Constraints)

1. **사내 고유 명칭 하드코딩 금지**:
   - 서비스 이름이나 이벤트 로그 이름에 특정 회사명(`Feelanet-` 등)을 하드코딩하지 마세요. 기본값은 `PSJobScheduler` / `PSJobScheduler-Logs`를 사용하고 파라미터로 변경 가능하게 유지합니다.
2. **`nssm.exe` 및 샘플 작업(`jobs/*.ps1`) 유지**:
   - `nssm.exe`는 폐쇄망/내부망 Windows 서버에서 외부 다운로드 없이 즉시 서비스 등록을 수행하기 위한 필수 바이너리이므로 삭제하지 마세요.
   - `jobs/` 하위의 6개 샘플 스크립트는 사용자가 Cron-Safe 문법을 학습하고 `Test-CronSchedule.ps1` 및 `Get-JobStatus.ps1`를 즉시 테스트할 수 있는 기준 파일이므로 유지합니다.
3. **런타임 생성 폴더(`logs/`, `state/`, `schedule.stop`) 커밋 주의**:
   - 테스트 실행 중 생성된 `logs/`, `state/`, `schedule.stop` 파일은 Git에 커밋되지 않도록 `.gitignore`에 등록되어 있어야 합니다.
4. **PID 재사용 방지 및 Graceful Shutdown 보존**:
   - `Repair-StaleLock`의 프로세스 `StartTime` 비교 로직과 `schedule.stop` 기반 종료 대기 로직을 임의로 단순화하지 마세요.
