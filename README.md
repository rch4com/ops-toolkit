# ops-toolkit

사내 인프라 및 시스템 운영 관리를 위한 유틸리티 모음 (Internal scripts & utilities for daily operations and system administration)

- **Runtime Stack**: **PowerShell 7 (`pwsh`)** + **C# (.NET 10)**
- **Development Workflow**: Multi-Agent AI-Native SDLC (`AGENTS.md` → `INTENT.md` → `SPEC.md` → `PLAN.md` → `REVIEW.md`)

---

## 프로젝트 취지 (Why `ops-toolkit`?)

개발 및 시스템 운영 업무를 수행하다 보면 다양한 트러블슈팅, 상태 진단, 반복 작업 자동화가 필요합니다. 이때 개인 PC에만 머물러 있던 일회성 스크립트나 분석 도구들을 **누구나 쉽게 찾고, 안심하고 재사용하며, 함께 발전시킬 수 있도록 공유**하기 위해 이 저장소를 만들었습니다.

- **문제 발견 및 진단 (Diagnostics & Troubleshooting)**: 개발/운영 중 발생하는 네트워크, 프로세스, 로그, 인프라 이슈를 빠르게 파악하고 원인을 찾는 진단 도구 제공
- **문제 해결 및 자동화 (Resolution & Automation)**: 반복적인 운영 작업이나 장애 조치를 표준화하고, 사전 시뮬레이션(`-WhatIf` / `--dry-run`)을 통해 휴먼 에러 없이 안전하게 해결
- **지식 자산화 및 공유 (Sharing & Reusability)**: 한 사람이 겪고 해결한 도구를 동료 개발자 및 운영자들과 공유하여, 팀 전체가 같은 문제를 다시 겪을 때 즉시 꺼내 쓸 수 있는 **공용 공구함(Toolkit)** 역할 수행

---

## 1. 저장소 구조 (Repository Structure)

```text
ops-toolkit/
├── AGENTS.md                        # 모든 AI 코딩 에이전트 공통 개발 헌법
├── INTENT.md                        # 프로젝트 목적, 언어 선택 기준, 운영 안전 원칙
├── SPEC.md                          # 기술 아키텍처 및 스크립트/CLI 표준 명세
├── PLAN.md                          # 단계별 구현 계획 및 진행 로드맵
├── REVIEW.md                        # 완료 전 필수 보안/품질/안전성 체크리스트
├── CLAUDE.md                        # Claude Code 진입점
├── GEMINI.md                        # Gemini CLI / Antigravity 진입점
├── Directory.Build.props            # .NET 10 공통 빌드 설정
├── global.json                      # .NET 10 SDK 버전 정책
├── .editorconfig                    # C# / PowerShell 인코딩 및 코드 스타일 표준
├── scripts/                         # PowerShell 7 (pwsh) 스크립트 및 모듈
│   ├── modules/                     # 공통 PowerShell 모듈 (예: OpsCommon)
│   └── <category>/<ToolName>/       # 개별 PowerShell 도구 (코드 + 6대 필수 문서 포함)
├── src/                             # C# (.NET 10) CLI 애플리케이션 및 공통 라이브러리
│   ├── OpsToolkit.Shared/           # 공통 라이브러리
│   └── <ToolName>/                  # 개별 C# CLI 도구 (코드 + 6대 필수 문서 포함)
├── tests/                           # 자동화 테스트
│   ├── powershell/                  # Pester 테스트 (*.Tests.ps1)
│   └── dotnet/                      # xUnit 테스트 (*.Tests.csproj)
└── docs/
    └── templates/                   # 신규 유틸리티 기획용 6대 문서 템플릿
```

---

## 2. 신규 유틸리티 추가 표준 워크플로우 (인터뷰 기반 6대 문서 필수화)

이 저장소는 여러 AI 코딩 CLI 도구를 동시에 활용하여 개발할 수 있도록 구성되어 있습니다.
특히 **다른 동료 개발자나 운영자가 각 프로그램의 제작 취지와 사용법을 명확히 알고 재사용**할 수 있도록, 새로운 유틸리티나 PowerShell 프로그램을 추가할 때는 **반드시 AI 에이전트와 사전 인터뷰(질의응답)**를 거쳐 해당 도구 폴더 내에 아래 **6개 문서**를 기본으로 생성해야 합니다.

### 개별 도구 폴더 내 필수 생성 파일 (6종)
| 파일명 | 대상 독자 | 주요 역할 및 포함 내용 |
| :--- | :--- | :--- |
| **`README.md`** | 동료 사용자 / 운영자 | 도구의 목적(취지), 설치 및 실행 방법, 파라미터/옵션 설명, 실전 예제 |
| **`AGENTS.md`** | AI 코딩 에이전트 | 해당 도구 수정 시 필요한 로컬 빌드/실행/테스트 명령어 및 도메인 주의사항 |
| **`INTENT.md`** | 기획자 / 에이전트 | 인터뷰를 통해 정리한 해결 과제(Why), 기대 결과, 안전 제약(Non-Goals) |
| **`SPEC.md`** | 개발자 / 에이전트 | 입출력 파라미터 스키마, `-WhatIf` / `--dry-run` 동작, 종료 코드 등 기술 명세 |
| **`PLAN.md`** | 개발자 / 에이전트 | 단계별 구현 체크리스트 및 검증 계획 |
| **`REVIEW.md`** | 리뷰어 / 에이전트 | 비밀정보 미포함, Dry-Run 동작, 정적 분석 및 테스트 통과 검수 기록 |

### 진행 순서
1. **요구사항 인터뷰**: AI 에이전트에게 새 도구를 만들겠다고 요청하면, 에이전트가 문제 상황·대상 환경·입출력·안전장치 여부를 인터뷰합니다.
2. **6대 문서 생성**: 인터뷰 결과를 바탕으로 도구 폴더(`src/<ToolName>/` 또는 `scripts/<category>/<ToolName>/`)에 6개 문서를 먼저 작성합니다 ([템플릿 참고](./docs/templates/TOOL_BLUEPRINT.template.md)).
3. **코드 및 테스트 구현**: 확정된 `SPEC.md`와 `PLAN.md`에 맞춰 코드를 구현하고 단위 테스트를 작성합니다.
4. **검수 및 카탈로그 등록**: `REVIEW.md` 체크리스트를 통과한 뒤 아래 유틸리티 카탈로그에 등록합니다.

---

## 3. 유틸리티 카탈로그 (Tool Catalog)

### PowerShell Scripts (`scripts/`)
| 카테고리 | 스크립트/도구명 | 경로 | 설명 | 호환 버전 | Dry-Run 지원 |
| :--- | :--- | :--- | :--- | :---: | :---: |
| `iis` | **[Remove-ExpiredIISCertificate](./scripts/iis/Remove-ExpiredIISCertificate/README.md)** | `scripts/iis/Remove-ExpiredIISCertificate/` | IIS 및 HTTP.sys 바인딩을 전수 확인하여 만료된 미바인딩 인증서만 안전하게 조회/삭제 및 현재 폴더 하위 CSV 리포트 생성 | PS 5.1 / 7+ | O (`-List`, `-Execute -WhatIf`) |
| `scheduler` | **[PSJobScheduler](./scripts/scheduler/PSJobScheduler/README.md)** | `scripts/scheduler/PSJobScheduler/` | 파일명 기반 Cron-Safe(`X`, `@`, `L`) 표기로 PowerShell 작업을 주기 실행하며 타임아웃, 서킷 브레이커, 재시도, NSSM Windows 서비스 등록 지원 | PS 5.1 / 7+ | O (`-WhatIf`, `-RunOnce`, `Test-CronSchedule`) |
| `modules` | `OpsCommon` (예정) | `scripts/modules/` | 공통 로깅 및 운영 안전 헬퍼 모듈 | PS 7+ | - |

### C# .NET 10 CLI Tools (`src/`)
| 도구명 | 경로 | 설명 | Dry-Run 지원 |
| :--- | :--- | :--- | :---: |
| **[LdapAuthTester](./src/LdapAuthTester/README.md)** | `src/LdapAuthTester/` | Active Directory 도메인 조인 감지 및 LDAP(389)/LDAPS(636) 연결성·인증 검증 하이브리드(WinForms GUI + CLI) 도구 (계정 잠금 방지 및 3종 표준 바인딩 지원) | O (`--dry-run`, GUI 사전 포트 점검) |
| `OpsToolkit.Shared` (예정) | `src/OpsToolkit.Shared` | CLI 공통 옵션 및 출력 포맷 라이브러리 | - |

---

## 4. 빠른 시작 (Quick Start)

### 사전 요구 사항
- **PowerShell 7+ (`pwsh`)**: `winget install Microsoft.PowerShell`
- **.NET 10 SDK**: `winget install Microsoft.DotNet.SDK.10`

### 빌드 및 테스트
```powershell
# .NET 10 CLI 도구 전체 빌드 및 테스트
dotnet build -warnaserror
dotnet test

# PowerShell 7 정적 분석 및 Pester 테스트
Invoke-ScriptAnalyzer -Path .\scripts\ -Recurse -Severity Warning,Error
Invoke-Pester -Path .\tests\powershell\ -Output Detailed
```
