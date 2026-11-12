# CLAUDE.md — Claude Code Instructions

이 저장소(`ops-toolkit`)는 멀티 AI 에이전트 공통 표준인 `AGENTS.md` 및 AI-Native SDLC 문서 체계를 따릅니다.
작업을 시작하기 전에 아래 파일들을 참조하여 동일한 컨벤션과 안전 수칙을 준수하세요.

- @AGENTS.md
- @INTENT.md
- @SPEC.md
- @PLAN.md
- @REVIEW.md

## 핵심 요약
1. **기술 스택**: PowerShell 7+ (`pwsh`) 및 C# (**.NET 10**, `net10.0`)
2. **안전 원칙**: 상태 변경 작업 시 PowerShell은 `SupportsShouldProcess`(`-WhatIf`), C# CLI는 `--dry-run`을 필수로 구현합니다. 비밀번호/IP/토큰 하드코딩을 엄격히 금지합니다.
3. **신규 도구 추가 규칙 (인터뷰 기반 6대 문서 필수)**:
   - 새로운 유틸리티나 PowerShell 프로그램을 추가할 때는 코드를 먼저 작성하지 말고 **사용자와 인터뷰(질의응답)**를 진행하세요.
   - 인터뷰 결과를 바탕으로 해당 도구 폴더 내에 **`README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`** 6개 파일을 반드시 생성하여 다른 사용자도 프로그램의 취지와 사용법을 알 수 있게 하세요.
