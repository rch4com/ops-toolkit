# SPEC.md — PSJobScheduler

## 1. 컴포넌트 및 파일 구성 명세

| 파일명 | 역할 | SupportsShouldProcess (`-WhatIf`) | 주요 파라미터 |
| :--- | :--- | :---: | :--- |
| `schedule.ps1` | 메인 스케줄러 루프 및 러너 생성/관리 | O | `-BaseDir`, `-TickSeconds`, `-MaxParallel`, `-EventLogName`, `-EventSource`, `-RunOnce` |
| `schedule-service-host.ps1` | NSSM 서비스용 호스트 래퍼 (`schedule.stop` 연동) | - | `-BaseDir`, `-ShutdownTimeoutSec` |
| `Install-PSSchedule-NSSM.ps1` | NSSM 기반 Windows 서비스 설치 및 시작 | O | `-ServiceName`, `-DisplayName`, `-Description`, `-EventLogName`, `-EventSource`, `-BaseDir` |
| `Uninstall-PSSchedule-NSSM.ps1` | Graceful Shutdown 요청 후 Windows 서비스 제거 | O | `-ServiceName`, `-BaseDir` |
| `Get-JobStatus.ps1` | 작업별 상태/마지막 실행/다음 실행/서킷 대시보드 | - | `-BaseDir`, `-PassThru` |
| `Test-CronSchedule.ps1` | 파일명 Cron-Safe 파싱 및 다음 실행 시각(`NextRun`) 검증 | - | `-FileNames`, `-BaseDir`, `-Now`, `-PassThru` |
| `nssm.exe` | Windows 서비스 등록/관리 실행 바이너리 | - | - |
| `jobs/*.ps1` | Cron-Safe 스케줄링 샘플 작업 스크립트 6종 | - | - |

---

## 2. Cron-Safe 파일명 파싱 계약 (`ConvertFrom-JobFileName`)

### 2.1 파일명 구조
```text
<Prefix(작업명)> <Min> <Hour> <Dom> <Mon> <Dow> [옵션1=값1] [옵션2=값2] ... .ps1
```
- 공백(`\s+`)으로 토큰을 분리한 뒤, 맨 뒤에서부터 정규식 `^(TIMEOUT|CB|NOW|LOG|CONCURRENT)=(.+)$`에 매칭되는 옵션 토큰들을 먼저 추출합니다.
- 남은 토큰 중 뒤에서 5개 토큰을 순서대로 `Min`, `Hour`, `Dom`, `Mon`, `Dow`로 파싱하고, 앞쪽 나머지 토큰들을 결합하여 `Prefix`(작업 이름)로 사용합니다.

### 2.2 반환 객체 (`PSCustomObject`)
- `Prefix` (`[string]`): 작업 이름
- `Cron` (`[pscustomobject]`): `Min`, `Hour`, `Dom`, `Mon`, `Dow` 문자열
- `TimeoutSec` (`[int]`): 기본값 `0`
- `CircuitN` (`[int]`): 기본값 `5`
- `NowOpt` (`[bool]`): 기본값 `$false`
- `LogOpt` (`[bool]`): 기본값 `$false`
- `Concurrent` (`[bool]`): 기본값 `$false`

---

## 3. 상태 관리 및 복구 명세 (`state/`)

- **Job Key**: 작업 파일 전체 경로(`FullName.ToLowerInvariant()`)의 UTF-8 바이트에 대한 `SHA256` 해시 문자열(소문자 64자, 메모리 캐싱 적용)
- **상태 파일 (`state/job_<JobKey>.json`)**:
  - `NextRun`, `RetryCount`, `NextRetry`, `LastExitCode`, `LastRun`, `PendingPid`, `PendingStartTime`, `PendingRunId`, `ConsecutiveFailures`, `CircuitOpenUntil`, `NowConsumed`
- **락 파일 (`state/job_<JobKey>.lock`)**:
  - 작업 시작 시 생성, 백그라운드 감시 Job이 프로세스 종료 시 자동 삭제
  - `Repair-StaleLock`: `PendingPid` 프로세스가 존재하지 않거나 `StartTime`이 `PendingStartTime`과 다르면(PID 재사용) Stale Lock으로 판단하여 즉시 제거
- **완료 파일 (`state/complete_<JobKey>_<RunId>.json`)**:
  - `runner_<JobKey>.ps1` 또는 타임아웃 워치독(`ExitCode = 124`, `Timeout = $true`)이 기록하며, 메인 루프가 수집하여 재시도(`1분, 5분, 15분`) 또는 서킷 브레이커(`CircuitOpenUntil`) 상태를 갱신한 뒤 삭제

---

## 4. 서비스 및 이벤트 로그 기본값 명세 (범용 명칭)

- **Windows 서비스 기본값**:
  - `ServiceName`: `PSJobScheduler`
  - `DisplayName`: `PowerShell Job Scheduler (Cron-Safe)`
  - `Description`: `Cron-Safe 파일명 기반 PowerShell 작업 스케줄러 서비스`
- **Windows 이벤트 로그 기본값**:
  - `EventLogName`: `PSJobScheduler-Logs`
  - `EventSource`: `PSJobScheduler`
  - 주요 Event ID:
    - `3001`: 스케줄러 시작
    - `3002`: 중지 파일(`schedule.stop`) 감지
    - `3003`: 스케줄러 정상 종료
    - `3100`: 개별 작업 시작
    - `3103`: 개별 작업 프로세스 시작 실패
    - `3200`: 개별 작업 성공 (`ExitCode = 0`)
    - `3201`: 개별 작업 타임아웃 강제 종료 (`ExitCode = 124`)
    - `3202`: 개별 작업 실행 실패 (`ExitCode != 0`)
    - `3301`: 연속 실패 임계치 도달로 서킷 브레이커 OPEN
