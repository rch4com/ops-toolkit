# AGENTS.md — Publish-SingleFileTools

이 도구는 루트 [AGENTS.md](../../../AGENTS.md)의 규칙을 따릅니다.
작업 전 현재 폴더의 `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md`를 먼저 확인하세요.

## 로컬 빌드 및 실행 명령어
- Dry-Run 검증:
  ```powershell
  pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -WhatIf
  ```
- 실제 단일 파일 게시 실행:
  ```powershell
  pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -ToolName LdapAuthTester
  ```
- 정적 분석 및 테스트 실행:
  ```powershell
  Invoke-ScriptAnalyzer -Path .\scripts\build\Publish-SingleFileTools\ -Recurse -Severity Warning,Error
  Invoke-Pester -Path .\tests\powershell\Publish-SingleFileTools.Tests.ps1 -Output Detailed
  ```

## 도구별 특수 주의사항
- `#Requires -Version 7.0` 및 `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`을 유지하세요.
- 별칭(Alias)을 절대 사용하지 마세요.
- `$PSCmdlet.ShouldProcess(...)` 분기에서 `-WhatIf` 실행 시 디스크에 디렉터리나 파일을 생성하지 않고 `Status = 'WhatIf'`를 포함한 `[PSCustomObject]`를 반환해야 합니다.
