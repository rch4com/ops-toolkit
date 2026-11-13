# REVIEW.md — Code Review & Quality Gate Checklist

## 2026-11-13 Utilities 통합 검증

- 범위: Phase 1 AD 진단/선택적 저장, Phase 2 UI 독립 5서비스, Phase 4 네이티브 Nocturne. 스캐폴더 제외. 신규 도구의 6문서를 먼저 작성하고 기존 도구의 명세/계획을 갱신했다.
- `dotnet build -warnaserror`: 경고 0, 오류 0.
- `dotnet test --nologo`: Shared 14, LDAP 36, MIP core 502, Explorer 91, 총 643/643 통과.
- `Invoke-Pester -Path ./tests/powershell/ -Output Normal -PassThru`: 33/33 통과 (신규 AD 11 포함).
- `node --test design-system/tests/*.test.cjs`: 대비 검사 3/3 통과.
- 변경 C# 전체 `dotnet format ./OpsToolkit.slnx --verify-no-changes --no-restore --include <changed files>`: 통과.
- 전체 `dotnet format --verify-no-changes --no-restore`: 실패. 이번에 수정하지 않은 MIP 코어 `Internal/Pdf/PdfLexer.cs` 등의 기존 들여쓰기 및 `tests/dotnet/MIPLabelCustomCheck.Tests/LabelDetailTests.cs` 등의 줄끝 오류가 남아 있다. 해당 예시 파일은 HEAD와 동일함을 `git diff --quiet`로 확인했다. 무관한 대규모 재포맷은 하지 않았다.
- PSScriptAnalyzer Warning/Error: 신규 AD 스크립트 0건, 기존 스크립트 115건. 기존 스크립트 변경 없음.
- 독립 리뷰 후 알 수 없는 JSON envelope 필드가 사라질 수 있는 문제를 실패 테스트로 재현하고 거부/원본 보존하도록 수정했다. 폼 종료 중 비동기 저장 수명 문제도 회귀 테스트로 검증했다.
- GUI: MIP 라이트/다크 96 DPI STA 렌더, LDAP 현재 OS 테마의 그룹 배치/색상 렌더를 검토했다. 캡처는 `artifacts/nocturne-review/` (추적 제외). 실제 창 자동 실행은 도구 오류로 실패하여 대화형 키보드·스크린리더·고대비 전환·다양한 DPI는 미확인이다. WinForms DrawToBitmap은 일부 입력 컨트롤을 완전히 그리지 못한다.
- 실제 AD 인증·외부 서버·실제 문서 스캔은 실행하지 않았다. 저장 서비스는 파일당 단일 소유자 전제이며 동기 Windows bind는 즉시 취소되지 않을 수 있다. 상세 제한은 각 도구 REVIEW/README 참조.
- Utilities/Modubit 디자인 출처를 보존했으며 원본에 없는 라이선스를 새로 주장하지 않았다. 원본 저장소 및 기존 graphify 산출물은 이번 구현에서 수정하지 않았다.

상태: 구현 및 범위 내 자동 검증 완료. 저장소 전체 포맷/기존 정적 분석 부채와 실환경 대화형 검증은 남아 있다.

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
