# AGENTS.md — Remove-ExpiredIISCertificate

이 도구는 루트 [AGENTS.md](../../../AGENTS.md)의 규칙을 따르며, 현장 서버 호환성을 위해 **Windows PowerShell 5.1 및 PowerShell 7+ (`pwsh`) 겸용**으로 유지관리됩니다.
코드를 수정하기 전에 현재 폴더의 [INTENT.md](./INTENT.md), [SPEC.md](./SPEC.md), [PLAN.md](./PLAN.md), [REVIEW.md](./REVIEW.md)를 반드시 확인하세요.

---

## 1. 로컬 실행 및 테스트 명령어

```powershell
# 도움말 출력 확인 (비관리자 권한에서도 동작)
pwsh -NoProfile -File .\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1 -Help

# PSScriptAnalyzer 정적 분석
Invoke-ScriptAnalyzer -Path .\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1 -Severity Warning,Error

# Pester 단위 테스트 실행
Invoke-Pester -Path .\tests\powershell\Remove-ExpiredIISCertificate.Tests.ps1 -Output Detailed
```

---

## 2. 도구 고유 주의사항 (Critical Constraints)

1. **PowerShell 5.1 & 7+ 호환성 유지**:
   - `#Requires -Version 5.1`을 유지하고, 삼항 연산자(`? :`)나 null 병합 연산자(`??`) 등 PowerShell 7 전용 문법을 사용하지 마세요.
   - CSV 내보내기 시 PowerShell 5.1에서도 한글이 깨지지 않도록 `UTF8` 인코딩(BOM 포함 호환) 처리를 유지하세요.
2. **바인딩 보호 로직 절대 훼손 금지**:
   - `Get-ProtectedThumbprints`의 CCS 바인딩 감지 시 즉시 중단(`throw`), 빈 해시 감지 시 중단, `netsh http show sslcert` 종료 코드 검사 로직을 완화하거나 제거하지 마세요.
3. **로그 기본 경로 규칙**:
   - `-LogDirectory` 기본값은 특정 드라이브(`C:\Logs\...`)가 아니라 **현재 실행 경로(`$PWD`) 하위의 `Logs` 폴더**여야 합니다.
