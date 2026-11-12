# PLAN.md — Implementation Plan & Roadmap

> **문서 역할**: `INTENT.md`와 `SPEC.md`에 정의된 설계를 바탕으로 **실행 가능한 단계별 작업(Task)과 검증 방법**을 관리하는 문서입니다. 에이전트는 작업 진행에 따라 체크박스(`[x]`)를 업데이트합니다.

---

## Phase 1: 저장소 기반 및 Multi-AI CLI 거버넌스 구축 (현재 단계)

- [x] **Task 1.1: AI-Native SDLC 핵심 문서 작성**
  - `AGENTS.md`: 에이전트 공통 코딩 규칙, PowerShell 7 / .NET 10 표준 정의
  - `INTENT.md`: 프로젝트 목적, 언어 선택 기준, 운영 안전 원칙 정의
  - `SPEC.md`: 기술 아키텍처, 스크립트/CLI 템플릿 명세 정의
  - `PLAN.md`: 단계별 실행 계획 수립
  - `REVIEW.md`: 품질/보안/운영 안전성 리뷰 체크리스트 작성
- [x] **Task 1.2: 멀티 CLI 브릿지 설정**
  - `CLAUDE.md` 생성 (Claude Code 연동)
  - `GEMINI.md` 생성 (Gemini CLI / Antigravity 연동)
  - `.github/copilot-instructions.md` 생성 (GitHub Copilot 연동)
- [x] **Task 1.3: .NET 10 및 PowerShell 7 빌드/스타일 환경 구성**
  - `global.json`: .NET 10 SDK(`10.0.100` 이상, `latestFeature` 롤포워드) 지정
  - `Directory.Build.props`: `net10.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` 설정
  - `.editorconfig`: C# 및 PowerShell 파일 포맷팅 표준 정의
- [x] **Task 1.4: 기본 디렉터리 스캐폴딩 및 작업 템플릿 생성**
  - `scripts/modules/`, `src/`, `tests/powershell/`, `tests/dotnet/` 구조 생성
  - `docs/templates/` 하위에 신규 유틸리티 기획용 템플릿 파일 구성
  - `README.md` 업데이트

---

## Phase 2: 공통 기반 모듈 및 라이브러리 구현 (차기 단계)

- [ ] **Task 2.1: PowerShell 공통 모듈 (`scripts/modules/OpsCommon`) 개발**
  - 표준 로깅 함수 (`Write-OpsLog`), 실행 결과 레코드 생성기, 공통 사전 점검 함수 구현
  - `tests/powershell/OpsCommon.Tests.ps1` Pester 테스트 작성
- [ ] **Task 2.2: .NET 10 솔루션 및 공통 라이브러리 (`src/OpsToolkit.Shared`) 구성**
  - `OpsToolkit.slnx` (또는 `.sln`) 생성
  - CLI 공통 옵션(`--dry-run`, `--verbose`, `--json`) 및 콘솔 출력 헬퍼 구현
  - `tests/dotnet/OpsToolkit.Shared.Tests` xUnit 테스트 작성

---

## Phase 3: 개별 운영 유틸리티 개발 워크플로우 (반복 단계)

새로운 스크립트(`scripts/<category>/<ToolName>/`)나 CLI 도구(`src/<ToolName>/`)를 추가할 때마다 아래 순서를 엄격히 따릅니다:

1. **요구사항 인터뷰 (Interactive Interview)**:
   - 코드를 먼저 작성하지 않고, 에이전트가 사용자에게 도구의 취지(해결하려는 문제), 구현 형태(PowerShell 7 vs C# .NET 10), 입출력 파라미터, 상태 변경 및 안전장치(`-WhatIf` / `--dry-run`) 범위를 질문합니다.
2. **도구별 6대 필수 문서 생성**:
   - 인터뷰 결과를 바탕으로 해당 도구 폴더 내에 다음 6개 문서를 기본 생성합니다 (`docs/templates/TOOL_BLUEPRINT.template.md` 참조):
     - `README.md` (다른 사용자를 위한 프로그램 취지 및 사용법/예제)
     - `AGENTS.md` (도구 전용 에이전트 빌드/테스트 지침)
     - `INTENT.md` (기획 의도 및 제약사항)
     - `SPEC.md` (입출력 파라미터 및 기술 명세)
     - `PLAN.md` (구현 단계 체크리스트)
     - `REVIEW.md` (보안 및 품질 검수 체크리스트)
3. **구현 및 단위 테스트 작성**:
   - 확정된 `SPEC.md`와 `PLAN.md`에 맞춰 코드를 구현하고 `tests/` 하위에 대응하는 테스트 코드를 작성합니다.
4. **검증 및 카탈로그 등록 (`REVIEW.md`)**:
   - 빌드/린트/테스트 명령어를 실행하고 도구 폴더의 `REVIEW.md` 체크리스트를 통과한 뒤, 루트 `README.md`의 유틸리티 카탈로그에 등록합니다.
