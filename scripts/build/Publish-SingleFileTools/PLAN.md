# PLAN.md — Publish-SingleFileTools

- [x] Step 1: 인터뷰 기반 6대 문서(`README`, `AGENTS`, `INTENT`, `SPEC`, `PLAN`, `REVIEW`) 생성
- [x] Step 2: 파라미터 바인딩(`ToolName`, `Runtime`, `Configuration`, `OutputDirectory`, `SelfContained`) 및 `SupportsShouldProcess`(`-WhatIf`) 뼈대 구현
- [x] Step 3: `dotnet publish` 단일 파일 패키징 로직 및 `[PSCustomObject]` 파이프라인 출력 구현
- [x] Step 4: Pester v5 단위 테스트(`tests/powershell/Publish-SingleFileTools.Tests.ps1`) 작성 및 검증
- [x] Step 5: `REVIEW.md` 검수 및 루트 `README.md` 카탈로그 등록
