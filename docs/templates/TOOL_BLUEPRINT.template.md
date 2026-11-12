# [도구/스크립트명] 6대 필수 문서 생성 가이드 및 템플릿

> **규칙**: `ops-toolkit`에 새로운 C# (.NET 10) 유틸리티(`src/<ToolName>/`) 또는 PowerShell 7 프로그램(`scripts/<category>/<ToolName>/`)을 추가할 때는 **반드시 사용자와의 인터뷰를 거친 뒤 아래 6개 파일을 해당 도구 폴더 내에 생성**해야 합니다.

---

## 인터뷰 질문 체크리스트 (AI 에이전트용)

코드를 작성하기 전에 사용자에게 다음 사항을 질문하여 명확히 파악하세요:
1. **배경 및 취지**: 어떤 개발/운영 문제를 찾거나(진단) 해결하려는 도구인가요? 다른 동료들이 어떤 상황에서 이 도구를 쓰게 되나요?
2. **구현 방식**: PowerShell 7 스크립트(`scripts/`)와 C# .NET 10 CLI(`src/`) 중 어느 것으로 구현할까요?
3. **입출력 사양**: 어떤 입력 파라미터/옵션을 받고, 결과는 어떻게 출력(콘솔 표, JSON, CSV, 파일 등)해야 하나요?
4. **안전장치**: 파일 삭제, 프로세스 종료, 설정 변경 등 상태를 바꾸는 작업이 있나요? (`-WhatIf` / `--dry-run` 적용 대상 확인)

---

## 파일 1: `README.md` (다른 사용자/동료를 위한 안내서)

```markdown
# <ToolName>

> 한 줄 요약: 이 도구가 어떤 문제를 찾거나 해결해 주는지 적습니다.

## 1. 제작 취지 (Why this tool?)
- 개발/운영 중 어떤 불편함이나 문제를 해결하기 위해 만들었는지 설명합니다.
- 어떤 상황에서 동료들이 이 도구를 사용하면 좋은지 안내합니다.

## 2. 실행 환경 및 사전 요구사항
- PowerShell 7 (`pwsh`) 또는 .NET 10 런타임
- 필요한 권한 (예: 일반 사용자 권한 / 로컬 관리자 권한)

## 3. 사용법 및 옵션 (Usage & Options)

| 옵션 / 파라미터 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :--- |
| `-Target` / `--target` | 필수 | - | 점검 또는 작업 대상 |
| `-WhatIf` / `--dry-run` | 선택 | `false` | 실제 변경 없이 수행 예정 작업만 시뮬레이션 |

## 4. 실행 예제 (Examples)

### 기본 진단/실행
(예제 명령어 작성)

### Dry-Run (안전 시뮬레이션)
(예제 명령어 작성)
```

---

## 파일 2: `AGENTS.md` (도구 전용 AI 에이전트 지침)

```markdown
# AGENTS.md — <ToolName>

이 도구는 루트 [AGENTS.md](../../AGENTS.md)의 규칙을 따릅니다.
작업 전 현재 폴더의 `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`를 먼저 확인하세요.

## 로컬 빌드 및 실행 명령어
- 실행: (명령어)
- Dry-Run 검증: (명령어)
- 테스트 실행: (명령어)

## 도구별 특수 주의사항
- (이 도구를 수정할 때 절대 건드리면 안 되는 로직이나 주의점 기록)
```

---

## 파일 3: `INTENT.md` (기획 의도 및 범위)

```markdown
# INTENT.md — <ToolName>

## 1. 해결하려는 문제 (Problem)
- 

## 2. 기대 결과 및 성공 기준 (Expected Outcome)
- 

## 3. Non-Goals (하지 않을 것 / 제약 사항)
- 
```

---

## 파일 4: `SPEC.md` (기술 명세)

```markdown
# SPEC.md — <ToolName>

## 1. 입출력 계약 (Input / Output Contract)
- **입력 파라미터**:
- **출력 포맷**: (`PSCustomObject` 스키마 또는 CLI JSON/콘솔 출력 형식)
- **종료 코드(Exit Code)**: `0`(성공), `1`(런타임 오류), `2`(인자 오류)

## 2. 핵심 동작 로직 및 Dry-Run (`-WhatIf` / `--dry-run`) 처리
- 

## 3. 예외 처리 (Error Handling)
- 
```

---

## 파일 5: `PLAN.md` (구현 계획)

```markdown
# PLAN.md — <ToolName>

- [ ] Step 1: 인터뷰 기반 6대 문서(`README`, `AGENTS`, `INTENT`, `SPEC`, `PLAN`, `REVIEW`) 생성 및 사용자 컨펌
- [ ] Step 2: 파라미터/옵션 바인딩 및 Dry-Run(`-WhatIf` / `--dry-run`) 뼈대 구현
- [ ] Step 3: 핵심 진단/해결 로직 구현
- [ ] Step 4: 단위 테스트(`tests/`) 작성 및 검증
- [ ] Step 5: `REVIEW.md` 검수 및 루트 `README.md` 카탈로그 등록
```

---

## 파일 6: `REVIEW.md` (품질 및 보안 검수)

```markdown
# REVIEW.md — <ToolName>

- [ ] **보안**: 코드 및 예제에 비밀번호, API 키, 사내 민감 IP가 하드코딩되지 않았는가?
- [ ] **안전성**: `-WhatIf` 또는 `--dry-run` 실행 시 실제 시스템 변경 없이 안전하게 동작하는가?
- [ ] **사용성**: 다른 사용자가 이 폴더의 `README.md`만 읽고도 도구의 취지와 사용법을 즉시 이해할 수 있는가?
- [ ] **품질**: `dotnet build -warnaserror` / `Invoke-ScriptAnalyzer` 및 단위 테스트를 통과했는가?
```
