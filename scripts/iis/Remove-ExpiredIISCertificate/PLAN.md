# PLAN.md — Remove-ExpiredIISCertificate

- [x] **Step 1: 요구사항 인터뷰 및 설계 확정**
  - PowerShell 5.1 & 7+ 겸용 유지
  - 표준 승인 동사 기반 경로 및 이름(`scripts/iis/Remove-ExpiredIISCertificate/Remove-ExpiredIISCertificate.ps1`) 채택
  - 기존 `-List` / `-Execute` 유지 + `SupportsShouldProcess`(`-WhatIf`, `-Confirm`) 및 `-PassThru` 지원
  - CSV 로그 기본 저장 위치를 현재 실행 폴더 하위(`.\Logs`)로 변경
  - 기존 `ETC/` 폴더 제거
- [x] **Step 2: 도구 폴더 내 6대 필수 문서 생성**
  - `README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md` 작성
- [x] **Step 3: `Remove-ExpiredIISCertificate.ps1` 리팩터링 및 구현**
  - Comment-Based Help 및 `Set-StrictMode -Version Latest` 적용
  - `$PSCmdlet.ShouldProcess` 적용 및 현재 폴더 하위 CSV 저장 구현
  - PowerShell 5.1 / 7+ 겸용 UTF-8 CSV 인코딩 처리
- [x] **Step 4: 원본 `ETC/` 폴더 삭제 및 Pester 단위 테스트 작성**
  - `ETC/Cleanup-ExpiredIISCertificates.ps1` 삭제
  - `tests/powershell/Remove-ExpiredIISCertificate.Tests.ps1` 작성 및 검증
- [x] **Step 5: `REVIEW.md` 검수 및 루트 `README.md` 카탈로그 등록**
