# REVIEW.md — Remove-ExpiredIISCertificate

## 1. 품질 및 안전성 체크리스트

- [x] **보안**: 코드 및 예제에 비밀번호, API 키, 사내 민감 IP가 하드코딩되지 않았는가?
- [x] **안전성 (`-WhatIf` & 바인딩 보호)**:
  - `-List` 및 `-Execute -WhatIf` 실행 시 실제 인증서 삭제가 발생하지 않는가?
  - IIS 및 `HTTP.sys`에 바인딩된 인증서가 삭제 후보에서 철저히 제외되는가?
  - 삭제 직전 재검증(TOCTOU 방어) 및 오류 시 즉시 중단 로직이 유지되었는가?
- [x] **로그 경로 요구사항**:
  - `-LogDirectory` 미지정 시 현재 실행하는 폴더 하위의 `Logs` 폴더에 CSV 파일이 생성되는가?
- [x] **호환성 및 코드 품질**:
  - PowerShell 5.1 및 PowerShell 7+ 문법 호환성을 준수하는가?
  - Approved Verb(`Remove-`) 및 `Set-StrictMode -Version Latest`가 적용되었는가?
  - Pester 단위 테스트(`tests/powershell/Remove-ExpiredIISCertificate.Tests.ps1`)를 통과했는가?
- [x] **문서화**:
  - 다른 사용자가 이 폴더의 `README.md`만 읽고도 도구의 취지와 사용법을 즉시 이해할 수 있는가?
  - 루트 `README.md`의 유틸리티 카탈로그에 등록되었는가?

---

## 2. 리뷰 기록

### [2026-11-12] Remove-ExpiredIISCertificate 초기 이관 및 표준화 리뷰
- **상태**: PASS
- **주요 변경 사항**:
  - `ETC/Cleanup-ExpiredIISCertificates.ps1`을 `scripts/iis/Remove-ExpiredIISCertificate/Remove-ExpiredIISCertificate.ps1`로 이관 및 표준화
  - `[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]` 및 `-PassThru` 추가
  - 기본 CSV 저장 경로를 현재 실행 위치 하위(`Join-Path (Get-Location).Path 'Logs'`)로 변경
  - Pester 테스트 작성 및 통과 완료
