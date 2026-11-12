# AGENTS.md — AI Coding Agent Guidelines for `ops-toolkit`

> **Single Source of Truth**: 이 문서는 `ops-toolkit` 저장소에서 작업하는 모든 AI 코딩 에이전트(Claude Code, Gemini CLI, Antigravity, OpenAI Codex, GitHub Copilot, Cursor 등)가 반드시 준수해야 하는 개발 헌법입니다.

---

## 1. 프로젝트 개요 및 참조 문서

`ops-toolkit`은 사내 인프라 및 시스템 운영 관리를 위한 **PowerShell 7 스크립트** 및 **C# (.NET 10) CLI 유틸리티** 모노레포입니다.

작업 시작 전 아래 AI-Native SDLC 문서들을 반드시 순서대로 확인하세요:
1. **[INTENT.md](./INTENT.md)**: 왜 만드는가? (프로젝트/작업의 목적, 운영 안전 원칙, Non-Goals)
2. **[SPEC.md](./SPEC.md)**: 무엇을 어떻게 만드는가? (기술 아키텍처, 입출력 계약, CLI 옵션 및 모듈 명세)
3. **[PLAN.md](./PLAN.md)**: 어떤 순서로 구현하는가? (단계별 체크리스트 및 검증 게이트)
4. **[REVIEW.md](./REVIEW.md)**: 완료 전 무엇을 검증하는가? (보안, 멱등성, 테스트, 코드 리뷰 체크리스트)

> 개별 도구/스크립트 개발 시에는 `docs/specs/<tool-name>/` 하위의 `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`를 우선 참조합니다.

---

## 2. 기술 스택 및 실행 환경

| 구분 | 표준 스택 | 비고 |
| :--- | :--- | :--- |
| **OS / Shell** | Windows / PowerShell 7+ (`pwsh`) | Windows PowerShell 5.1(`powershell.exe`) 비호환 문법 주의, `#Requires -Version 7.0` 명시 |
| **.NET 런타임** | **.NET 10 (`net10.0`)** / C# 14 | `Directory.Build.props` 및 `global.json` 기준. 단일 파일 스크립트형 도구는 `dotnet run app.cs` 활용 가능 |
| **PowerShell 테스트/린트** | `Pester` v5+, `PSScriptAnalyzer` | `tests/powershell/*.Tests.ps1` |
| **.NET 테스트** | `xUnit`, `dotnet test` | `tests/dotnet/*.Tests.csproj` |

---

## 3. 디렉터리 구조 규칙

```text
ops-toolkit/
├── AGENTS.md                        # 에이전트 공통 개발 지침 (현재 문서)
├── INTENT.md                        # 저장소 목적 및 현재 작업 의도 정의
├── SPEC.md                          # 기술 명세 및 아키텍처 표준
├── PLAN.md                          # 단계별 구현 계획 및 로드맵
├── REVIEW.md                        # 코드 리뷰 및 품질/보안 게이트 체크리스트
├── CLAUDE.md                        # Claude Code 브릿지
├── GEMINI.md                        # Gemini CLI / Antigravity 브릿지
├── Directory.Build.props            # .NET 10 공통 빌드 설정
├── global.json                      # .NET 10 SDK 버전 정책
├── .editorconfig                    # C# / PowerShell 코드 스타일 및 인코딩 규칙
├── scripts/                         # PowerShell 7 (pwsh) 스크립트 및 모듈
│   ├── modules/                     # 공통 모듈 (예: OpsCommon)
│   └── <category>/<ToolName>/       # 개별 PowerShell 도구 폴더 (코드 + 필수 6대 문서 세트)
├── src/                             # C# (.NET 10) CLI 애플리케이션 및 라이브러리
│   ├── OpsToolkit.Shared/           # 공통 유틸리티/로깅/설정 라이브러리
│   └── <ToolName>/                  # 개별 C# CLI 도구 폴더 (코드 + 필수 6대 문서 세트)
├── tests/                           # 자동화 테스트
│   ├── powershell/                  # Pester 테스트 (*.Tests.ps1)
│   └── dotnet/                      # xUnit 테스트 (*.Tests.csproj)
└── docs/
    └── templates/                   # 신규 유틸리티 기획용 6대 문서 템플릿
```

---

## 4. 코딩 컨벤션 및 필수 안전 수칙

### 4.1 운영 안전 제1원칙 (Safety & Idempotency)
- **자격 증명 하드코딩 절대 금지**: 비밀번호, API 키, 사내 주요 IP 대역, 연결 문자열 등을 코드에 직접 작성하지 마세요. 환경 변수(`$env:...`), `Microsoft.PowerShell.SecretManagement`, 또는 안전한 외부 설정 파일을 사용합니다.
- **파괴적 작업(변경/삭제/재시작) 안전장치 필수**:
  - **PowerShell**: 상태를 변경하는 모든 스크립트/함수는 `[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]`를 선언하고 `$PSCmdlet.ShouldProcess(...)`로 감싸 `-WhatIf`와 `-Confirm`이 동작하도록 작성합니다.
  - **C# CLI**: 변경 작업을 수행하는 모든 CLI 도구는 `--dry-run` 플래그를 필수로 지원해야 합니다.
- **멱등성(Idempotency)**: 동일 스크립트나 도구를 여러 번 실행해도 안전하도록 사전 상태 검사(Pre-check)를 포함합니다.

### 4.2 PowerShell 7 (`pwsh`) 규칙
- 파일 상단에 `#Requires -Version 7.0` 및 Comment-Based Help(`.SYNOPSIS`, `.DESCRIPTION`, `.PARAMETER`, `.EXAMPLE`)를 반드시 작성합니다.
- 스크립트 시작 시 `Set-StrictMode -Version Latest`와 `$ErrorActionPreference = 'Stop'`을 기본 설정합니다.
- 함수 및 스크립트 이름은 `Get-Verb`에 등록된 **Approved Verbs**(`Get-`, `Set-`, `Test-`, `Invoke-`, `Sync-`, `Export-` 등)와 `PascalCase` 명사를 조합합니다.
- 별칭(Alias) 사용 금지: `gci`, `%`, `?`, `ft`, ` select` 대신 `Get-ChildItem`, `ForEach-Object`, `Where-Object`, `Format-Table`, `Select-Object` 전체 이름을 사용합니다.
- 파이프라인 출력은 단순 텍스트(`Write-Host`)가 아닌 **구조화된 객체(`PSCustomObject`)**를 반환하여 다른 명령어와 조합(`| Export-Csv`, `| ConvertTo-Json`)할 수 있게 작성합니다. 진행 상황/정보 로깅은 `Write-Verbose` 또는 `Write-Information`을 사용합니다.

### 4.3 C# (.NET 10) 규칙
- `Directory.Build.props`에 정의된 `net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`를 준수합니다.
- 모든 I/O 및 네트워크 작업은 `async`/`await`로 작성하며, CLI 진입점부터 하위 메서드까지 `CancellationToken`(`Ctrl+C` 인터럽트 대응)을 반드시 전달합니다.
- 콘솔 출력 시 사람이 읽기 좋은 포맷과 기계가 파싱하기 좋은 포맷(`--output json` 또는 `--json`) 옵션을 함께 고려합니다.
- 종료 코드(Exit Code) 표준:
  - `0`: 성공
  - `1`: 일반 런타임 오류
  - `2`: 잘못된 인자/옵션 입력 (Usage Error)

---

## 5. 빌드, 테스트, 검증 명령어 (Common Commands)

코드를 작성하거나 수정한 후 작업을 완료 처리하기 전에 아래 명령어로 반드시 검증하세요.

### C# (.NET 10)
```powershell
# 전체 빌드 (경고를 오류로 처리)
dotnet build -warnaserror

# 코드 포맷 검사
dotnet format --verify-no-changes

# 단위 테스트 실행
dotnet test --nologo
```

### PowerShell 7 (`pwsh`)
```powershell
# 문법 및 정적 분석 (PSScriptAnalyzer 설치 시)
Invoke-ScriptAnalyzer -Path .\scripts\ -Recurse -Severity Warning,Error

# Pester 단위 테스트 실행
Invoke-Pester -Path .\tests\powershell\ -Output Detailed

# 변경 작업 스크립트 Dry-Run 검증 예시
pwsh -NoProfile -File .\scripts\<category>\<ScriptName>.ps1 -WhatIf
```

---

## 6. 신규 유틸리티/스크립트 추가 워크플로우 (인터뷰 기반 6대 필수 문서 생성)

> **[HARD GATE — 코드 선작성 금지]**
> 새로운 C# 유틸리티(`src/<ToolName>/`)나 PowerShell 프로그램(`scripts/<category>/<ToolName>/`)을 추가할 때는 **절대 코드를 먼저 작성하지 마세요.**
> 다른 동료 개발자나 운영자가 각 프로그램의 취지, 아키텍처, 사용법을 명확히 이해하고 재사용할 수 있도록 반드시 **사용자와 사전 인터뷰(질의응답)**를 진행한 뒤 해당 도구 폴더 내에 아래 **6개 필수 문서**를 먼저 생성해야 합니다.

### 6.1 개별 도구별 필수 생성 문서 (6종)
각 프로그램/스크립트 폴더(`src/<ToolName>/` 또는 `scripts/<category>/<ToolName>/`)에는 다음 파일들이 기본으로 포함되어야 합니다:
1. **`README.md`**: 다른 사용자를 위한 프로그램 소개, 해결하는 문제(취지), 설치/실행 방법, 옵션 표 및 실제 사용 예제
2. **`AGENTS.md`**: 해당 도구를 수정/유지보수할 때 AI 에이전트가 알아야 할 로컬 빌드/실행/테스트 명령어 및 도구 고유 주의사항
3. **`INTENT.md`**: 인터뷰를 통해 도출한 제작 배경(Why), 해결하려는 운영/개발 문제, 기대 결과, 안전 제약(Non-Goals)
4. **`SPEC.md`**: 입력 파라미터/CLI 옵션, 입출력 데이터 형식, `-WhatIf`/`--dry-run` 동작 정의, 예외 처리 및 종료 코드 명세
5. **`PLAN.md`**: 구현 단계별 체크리스트 및 테스트 계획
6. **`REVIEW.md`**: 보안(비밀정보 미포함), Dry-Run 검증, 정적 분석 및 단위 테스트 통과 여부를 기록하는 검수 문서

### 6.2 단계별 진행 절차
1. **Step 1 — 요구사항 인터뷰 (Interactive Interview)**:
   - 사용자에게 다음 핵심 질문을 던져 요구사항을 구체화합니다:
     1) *어떤 개발/운영 문제를 찾거나 해결하려는 도구인가요? (누가 어떤 상황에서 쓰나요?)*
     2) *PowerShell 7 스크립트와 C# (.NET 10) CLI 중 어떤 형태가 적합한가요?*
     3) *입력값(파라미터/옵션)과 기대하는 출력 형태(콘솔, JSON, CSV, 로그 등)는 무엇인가요?*
     4) *상태를 변경/삭제하는 작업이 포함되어 있나요? (안전장치 `-WhatIf` / `--dry-run` 범위 확인)*
2. **Step 2 — 6대 문서 초안 생성 및 컨펌**:
   - 인터뷰 결과를 바탕으로 해당 도구 폴더에 `README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`를 생성합니다 (`docs/templates/` 참고).
3. **Step 3 — 구현 및 단위 테스트**:
   - 확정된 `SPEC.md`와 `PLAN.md`에 따라 코드를 구현하고 `tests/`에 테스트를 작성합니다.
4. **Step 4 — Self-Review 및 카탈로그 등록**:
   - 도구 폴더의 `REVIEW.md` 체크리스트를 완료하고, 루트 `README.md`의 유틸리티 카탈로그 표에 신규 도구를 등록합니다.
