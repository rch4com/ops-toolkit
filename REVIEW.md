# REVIEW.md — Code Review & Quality Gate Checklist

> **문서 역할**: 개발 완료(PR 생성, 커밋, 또는 사용자에게 완료 보고) 전에 AI 에이전트와 개발자가 반드시 점검해야 하는 **품질, 보안, 운영 안전성 체크리스트**입니다.

---

## 1. 의도 및 명세 부합성 (Intent & Spec Alignment)

- [ ] **`INTENT.md` 부합 여부**: 구현된 기능이 당초 해결하려던 문제와 목적에 정확히 부합하는가?
- [ ] **Scope 준수 (YAGNI)**: 요청하지 않은 과도한 추상화, 불필요한 외부 NuGet 패키지/PowerShell 모듈이 추가되지 않았는가?
- [ ] **`SPEC.md` 계약 준수**: 정의된 CLI 옵션/파라미터 이름, 입출력 포맷, 종료 코드(Exit Code) 규격을 지켰는가?

---

## 2. 운영 안전성 및 보안 (Operational Safety & Security) — **CRITICAL**

- [ ] **비밀정보(Secrets) 미포함 확인**: 코드, 설정 파일, 테스트 데이터, 주석에 비밀번호, API 키, 토큰, 사내 민감 IP/호스트명이 하드코딩되어 있지 않은가?
- [ ] **Dry-Run / WhatIf 동작 검증**:
  - PowerShell 상태 변경 스크립트에 `[CmdletBinding(SupportsShouldProcess)]`가 적용되어 있고 `-WhatIf` 실행 시 실제 변경이 일어나지 않는가?
  - C# 상태 변경 CLI에 `--dry-run` 옵션이 구현되어 있는가?
- [ ] **멱등성(Idempotency)**: 동일한 인자로 두 번 연속 실행했을 때 중복 생성이나 치명적 오류 없이 안전하게 처리되는가?
- [ ] **입력값 검증**: 잘못된 경로, 빈 문자열, 존재하지 않는 서버명 등이 입력되었을 때 명확한 오류 메시지와 함께 즉시 중단(Fail-Fast)되는가?

---

## 3. 언어별 코드 품질 (Code Quality)

### PowerShell 7 (`scripts/`)
- [ ] 파일 최상단에 `#Requires -Version 7.0` 및 Comment-Based Help(`.SYNOPSIS`, `.EXAMPLE` 등)가 작성되었는가?
- [ ] `Set-StrictMode -Version Latest` 및 `$ErrorActionPreference = 'Stop'`이 선언되었는가?
- [ ] 축약 별칭(`%`, `?`, `gci`, `ft` 등) 없이 정식 Cmdlet 이름과 Approved Verbs를 사용했는가?
- [ ] 단순 문자열 출력(`Write-Host`) 대신 파이프라인 조합이 가능한 `[PSCustomObject]`를 반환하는가?

### C# / .NET 10 (`src/`)
- [ ] `dotnet build -warnaserror` 실행 시 경고(Warning) 및 오류(Error)가 `0`건인가?
- [ ] Nullable Reference Types 경고가 없으며, 비동기 메서드에 `CancellationToken`이 올바르게 전파되었는가?
- [ ] `IDisposable` / `IAsyncDisposable` 리소스(`HttpClient`, `FileStream`, 네트워크 연결 등)가 `using`을 통해 안전하게 해제되는가?

---

## 4. 테스트 및 문서화 (Testing & Documentation)

- [ ] **도구별 6대 필수 문서 생성 완료**: 신규 유틸리티/스크립트 폴더 내에 인터뷰를 통해 작성된 `README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md` 6개 파일이 모두 존재하는가?
- [ ] **타 사용자 친화적 `README.md`**: 다른 개발자/운영자가 코드 내부를 보지 않고도 도구의 제작 취지, 옵션, 실행 예제를 이해하고 즉시 사용할 수 있는가?
- [ ] **자동화 테스트 통과**: `dotnet test` 또는 `Invoke-Pester`가 모두 통과했는가?
- [ ] **루트 카탈로그 업데이트**: 루트 `README.md`의 유틸리티 카탈로그 표에 신규 도구가 등록되었는가?

---

## 5. 최근 리뷰 기록 요약 (Review Log Template)

> 에이전트가 작업 완료 보고 또는 자체 리뷰를 수행할 때 아래 양식을 활용할 수 있습니다.

```markdown
### [YYYY-MM-DD] <도구/작업명> 리뷰 결과
- **상태**: PASS / NEEDS_WORK
- **검증 명령어 결과**:
  - Build/Lint: Pass
  - Tests: Pass
  - DryRun/WhatIf Check: Pass
- **주요 확인 사항**:
  - (예: 자격 증명 환경변수 분리 확인 완료, CancellationToken 전파 확인 완료)
- **잔여 이슈 / 후속 작업**:
  - 없음
```
