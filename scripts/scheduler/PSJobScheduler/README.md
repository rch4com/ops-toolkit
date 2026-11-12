# PSJobScheduler — Cron-Safe PowerShell Job Scheduler

> Windows 파일명 제약(`*`, `/`)을 우회하는 **Cron-Safe 표기법(`X`, `@`, `L`)**을 사용하여, `jobs/` 폴더에 `.ps1` 스크립트를 넣어두는 것만으로 주기적 실행·타임아웃·서킷 브레이커·재시도·Windows 서비스 구동을 제공하는 경량 PowerShell 스케줄러입니다. (PowerShell 5.1 & 7+ 겸용)

---

## 1. 제작 취지 (Why this tool?)

Windows 서버에서 다수의 운영/배치 PowerShell 스크립트를 주기적으로 실행할 때 기본 **Windows 작업 스케줄러(Task Scheduler)**는 다음과 같은 불편함이 있습니다:
- 작업마다 GUI나 복잡한 XML로 등록해야 하므로 수십 개의 스크립트 주기를 한눈에 파악하거나 Git으로 버전 관리하기 어렵습니다.
- 스크립트가 무한 대기(Hang)에 빠졌을 때의 **타임아웃 강제 종료**, 연속 장애 시 시스템을 보호하는 **서킷 브레이커(Circuit Breaker)**, 실패 시 **단계별 재시도(Backoff)**, 작업별 **표준 출력/에러 일자별 분리 로깅**을 일관되게 구성하기 번거롭습니다.

**`PSJobScheduler`**는 별도의 DB나 설정 파일 없이 **파일 이름 자체가 스케줄이자 정책**이 되도록 설계되었습니다.
`jobs/` 폴더에 `백업작업 0 2 X X X TIMEOUT=1800 CB=3.ps1`처럼 파일을 넣으면 즉시 스케줄링이 활성화되며, `nssm.exe`를 통해 안정적인 Windows 서비스로 백그라운드 구동할 수 있습니다.

---

## 2. 구성 파일 및 디렉터리 구조

```text
scripts/scheduler/PSJobScheduler/
├── schedule.ps1                    # 메인 스케줄러 엔진 (-WhatIf, -RunOnce 지원)
├── schedule-service-host.ps1       # Windows 서비스 구동용 래퍼 (Graceful Shutdown 처리)
├── Install-PSSchedule-NSSM.ps1     # NSSM 기반 Windows 서비스 설치 스크립트 (-WhatIf 지원)
├── Uninstall-PSSchedule-NSSM.ps1   # NSSM 기반 Windows 서비스 제거 스크립트 (-WhatIf 지원)
├── Get-JobStatus.ps1               # 작업 상태/다음 실행 시각/서킷 상태 조회 대시보드 (-PassThru 지원)
├── Test-CronSchedule.ps1           # 파일명 Cron-Safe 문법 파싱 및 다음 실행 시각 사전 검증 도구
├── nssm.exe                        # Windows 서비스 등록용 Non-Sucking Service Manager 실행 파일
├── jobs/                           # 실행할 PowerShell(.ps1) 작업 폴더 (6개 샘플 기본 포함)
├── logs/                           # (실행 시 자동 생성) 스케줄러/작업/서비스 일자별 UTF-8 로그
└── state/                          # (실행 시 자동 생성) 작업별 상태(JSON), Lock, Completion 파일
```

---

## 3. 작업 등록 및 Cron-Safe 파일명 규칙

`jobs/` 폴더 안에 아래 규칙으로 `.ps1` 파일 이름을 지으면 스케줄러가 자동으로 인식합니다:

```text
<작업이름> <분> <시> <일(DOM)> <월> <요일(DOW)> [옵션들...].ps1
```

### 3.1 Cron-Safe 기호 표
Windows 파일명에 사용할 수 없는 `*`와 `/` 대신 `X`와 `@`를 사용합니다.

| 표준 Cron | Cron-Safe | 의미 | 예시 |
| :---: | :---: | :--- | :--- |
| `*` | **`X`** | 모든 값 (Wildcard) | `X` (매분, 매시, 매일 등) |
| `*/n` | **`X@n`** | 간격 (Step) | `X@5` (5분/5시간 간격), `1-30@2` |
| `,` | **`,`** | 목록 (List) | `1,15,30` |
| `-` | **`-`** | 범위 (Range) | `9-18`, `MON-FRI` |
| `L` | **`L` / `L-n`** | 해당 월의 마지막 날 (일 필드 전용) | `L` (말일), `L-1` (말일 하루 전) |

- **필드 범위**:
  1. **분(MIN)**: `0-59`
  2. **시(HOUR)**: `0-23`
  3. **일(DOM)**: `1-31`, `L`, `L-n`
  4. **월(MON)**: `1-12`
  5. **요일(DOW)**: `0-7` (0과 7은 일요일) 또는 `SUN`, `MON`, `TUE`, `WED`, `THU`, `FRI`, `SAT`
- **일(DOM)과 요일(DOW) 동시 지정 시**: 표준 Cron 규칙에 따라 둘 중 하나만 제한(`X`가 아님)되면 해당 조건만 검사하고, 둘 다 제한되면 **OR 조건**(둘 중 하나라도 맞으면 실행)으로 동작합니다.

### 3.2 작업별 부가 옵션 (파일명 끝에 공백으로 구분하여 추가)

| 옵션 | 기본값 | 설명 |
| :--- | :---: | :--- |
| `TIMEOUT=<초>` | `0` (무제한) | 지정한 시간(초)을 초과하면 작업 프로세스 트리를 강제 종료(`ExitCode=124`)하고 이벤트 로그 알람 기록 |
| `CB=<횟수>` | `5` | 연속 실패 횟수가 임계값에 도달하면 서킷 브레이커를 발동(Open)하여 기본 60분간 실행 차단 |
| `NOW=1` | `false` | 스케줄러 최초 시작 시 스케줄 시각과 무관하게 즉시 1회 실행 |
| `LOG=1` | `false` | 기본적으로 실패 시에만 남기는 상세 표준 출력/에러 로그(`출력.log`)를 성공 시에도 강제 저장 |
| `CONCURRENT=1` | `false` | 이전 실행이 아직 끝나지 않았더라도 다음 주기에 중복 실행 허용 |

---

## 4. 기본 제공 샘플 스크립트 (`jobs/`) 안내

처음 사용하시는 분들이 Cron-Safe 문법과 동작을 바로 확인하실 수 있도록 `jobs/` 폴더에 6개의 예제 스크립트가 포함되어 있습니다.

| 샘플 파일명 | 실행 주기 | 시연 기능 |
| :--- | :--- | :--- |
| `job_1min X@1 X X X X.ps1` | 매 1분마다 | 로컬호스트 Ping 하트비트 점검 시뮬레이션 |
| `job_5min X@5 X X X X.ps1` | 매 5분마다 | 주요 Windows 서비스(`Spooler` 등) 실행 상태 모니터링 |
| `daily_4am 0 4 X X X.ps1` | 매일 새벽 04:00 | 일일 임시 파일 정리 및 정기 유지보수 작업 |
| `weekday_9am 0 9 X X MON-FRI.ps1` | 평일(월~금) 09:00 | 디스크 여유 공간 및 사용자 현황 일일 리포트 생성 |
| `month_end 30 18 L X X TIMEOUT=600 CB=3.ps1` | 매월 말일(`L`) 18:30 | 월 마감 배치 + 10분 타임아웃(`TIMEOUT=600`) 및 3회 연속 실패 서킷 브레이커(`CB=3`) 시연 |
| `year_end 55 23 31 12 X.ps1` | 매년 12월 31일 23:55 | 연말 연간 데이터 아카이빙 배치 시뮬레이션 |

---

## 5. 사용법 및 실행 예제 (Usage & Examples)

### 1) 작업 파일명 문법 및 다음 실행 시각 사전 검증 (`Test-CronSchedule.ps1`)
새 작업을 만들거나 기존 `jobs/` 폴더의 스크립트들이 언제 실행될지 확인합니다 (변경 작업 없음):
```powershell
# jobs/ 폴더 내 모든 스크립트의 Cron 파싱 결과 및 다음 실행 시각(NextRun) 확인
.\scripts\scheduler\PSJobScheduler\Test-CronSchedule.ps1

# 특정 파일명 패턴을 직접 테스트
.\scripts\scheduler\PSJobScheduler\Test-CronSchedule.ps1 -FileNames "my_backup 0 2 X X MON-FRI TIMEOUT=300 CB=3"
```

### 2) 스케줄러 Dry-Run 시뮬레이션 (`-WhatIf` / `-RunOnce`)
실제로 하위 작업을 실행하지 않고 어떤 작업이 실행 대상인지 1회 점검합니다:
```powershell
.\scripts\scheduler\PSJobScheduler\schedule.ps1 -RunOnce -WhatIf
```

### 3) 콘솔 모드로 스케줄러 실행 및 안전한 종료 (Graceful Shutdown)
```powershell
# 콘솔에서 스케줄러 실행
.\scripts\scheduler\PSJobScheduler\schedule.ps1

# 다른 터미널에서 안전하게 종료 요청 (실행 중인 작업이 끝날 때까지 대기 후 종료)
New-Item -ItemType File .\scripts\scheduler\PSJobScheduler\schedule.stop
```

### 4) 작업 상태 대시보드 조회 (`Get-JobStatus.ps1`)
현재 등록된 작업들의 상태(`IDLE`, `RUNNING`, `CIRCUIT_OPEN`), 마지막 종료 코드, 연속 실패 횟수, 다음 실행 시각을 표로 확인합니다:
```powershell
# 콘솔 테이블 출력
.\scripts\scheduler\PSJobScheduler\Get-JobStatus.ps1

# 파이프라인 객체로 받아서 서킷이 열린 작업만 필터링
.\scripts\scheduler\PSJobScheduler\Get-JobStatus.ps1 -PassThru | Where-Object Status -eq 'CIRCUIT_OPEN'
```

### 5) Windows 서비스 등록 및 제거 (`nssm.exe` 활용, 관리자 권한 필요)
서버 재부팅 시에도 백그라운드에서 자동 시작되도록 Windows 서비스(기본 서비스명: `PSJobScheduler`)로 등록합니다:
```powershell
# 서비스 설치 시뮬레이션 (-WhatIf)
.\scripts\scheduler\PSJobScheduler\Install-PSSchedule-NSSM.ps1 -WhatIf

# 서비스 실제 설치 및 시작
.\scripts\scheduler\PSJobScheduler\Install-PSSchedule-NSSM.ps1

# 서비스 중지 (schedule.stop 기반 Graceful Shutdown 자동 수행)
Stop-Service -Name PSJobScheduler

# 서비스 제거
.\scripts\scheduler\PSJobScheduler\Uninstall-PSSchedule-NSSM.ps1
```

---

## 6. 실제 업무 활용 사례 (Use Cases)

1. **인사(HR) 데이터 기반 일일 AD 속성 갱신 (`0 2 X X X.ps1`)**: 매일 새벽 인사 CSV 데이터를 읽어 Active Directory 부서/직위/퇴사자 계정을 동기화
2. **핵심 웹 서비스 상태 주기적 감시 및 알람 (`X@5 X X X X.ps1`)**: 5분마다 주요 HTTP/HTTPS 엔드포인트를 점검하고 장애 시 Teams/Slack 웹훅 발송
3. **디스크 사용량 모니터링 및 임시/로그 자동 정리 (`0 X X X X.ps1`)**: 매시간 C 드라이브 여유 공간을 체크하여 임계치 미만일 때 오래된 로그/덤프 정리
4. **보안 컴플라이언스용 장기 미사용 계정 차단 (`0 9 X X MON.ps1`)**: 매주 월요일 09시에 90일 이상 미접속 계정을 스캔 및 비활성화
5. **심야 데이터베이스 및 공유 폴더 배치 백업 (`30 23 X X X TIMEOUT=1800.ps1`)**: 매일 밤 23:30에 백업 수행 및 30분 초과 시 타임아웃 강제 종료 및 알람

---

## 7. 로그 및 복구 메커니즘

- **스케줄러 메인 로그**: `logs\스케줄러_yyyyMMdd.log` (기본 14일 보관 후 자동 롤링 삭제)
- **작업 실행/출력 로그**: `logs\<작업명>\yyyyMMdd\job.log` 및 `출력.log` (한글 깨짐 방지를 위해 UTF-8 자동 변환 및 캐싱 적용)
- **Windows 이벤트 로그**: `PSJobScheduler-Logs` (Source: `PSJobScheduler`)에 작업 시작(3100), 성공(3200), 타임아웃(3201), 실패(3202), 서킷 오픈(3301) 기록
- **자동 복구 (Self-Healing)**: 비정상 종료 후 남은 Stale `schedule.stop`, Stale Lock(PID 및 프로세스 `StartTime` 대조로 PID 재사용 오판 방지), 고아 `runner_*.ps1` 및 `complete_*.json` 파일을 스케줄러가 자동으로 감지하여 정리합니다.
