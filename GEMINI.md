# GEMINI.md — Gemini CLI & Antigravity Instructions

이 저장소(`ops-toolkit`)는 멀티 AI 에이전트 공통 표준인 [AGENTS.md](./AGENTS.md) 및 AI-Native SDLC 문서 체계를 따릅니다.

## 필수 참조 문서 (Single Source of Truth)
작업 수행 전 반드시 다음 문서들을 확인하세요:
1. **[AGENTS.md](./AGENTS.md)**: 코딩 규칙, 디렉터리 구조, 빌드/테스트 명령어 (.NET 10 + PowerShell 7)
2. **[INTENT.md](./INTENT.md)**: 프로젝트 목적, 언어 선택 기준, 운영 안전 원칙 및 Non-Goals
3. **[SPEC.md](./SPEC.md)**: 기술 아키텍처, 스크립트/CLI 표준 템플릿 및 인터페이스 명세
4. **[PLAN.md](./PLAN.md)**: 단계별 구현 계획 및 진행 상황 체크리스트
5. **[REVIEW.md](./REVIEW.md)**: 완료 보고 전 필수 보안/품질/안전성 검증 게이트

## 핵심 개발 규칙
- **환경**: Windows, PowerShell 7 (`pwsh`), .NET 10 (`net10.0`)
- **신규 도구 추가 시 인터뷰 및 6대 문서 필수 생성**:
  - 새로운 유틸리티나 PowerShell 프로그램을 만들 때는 코드를 바로 작성하지 말고 **사용자와 인터뷰**를 통해 목적과 요구사항을 먼저 파악하세요.
  - 다른 사용자도 도구의 취지와 사용법을 명확히 알 수 있도록 각 도구 폴더 내에 **`README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`** 6개 문서를 기본으로 생성해야 합니다.
- **운영 안전성**: 모든 변경/삭제 스크립트 및 도구는 `-WhatIf` (PowerShell) 또는 `--dry-run` (C# CLI)을 지원해야 합니다.
- **검증**: 작업 완료 전 `dotnet build -warnaserror`, `dotnet test`, `Invoke-Pester` 등을 통해 동작을 확인하고 `REVIEW.md` 기준을 충족하는지 점검하세요.
