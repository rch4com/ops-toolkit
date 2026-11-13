# Utilities integration implementation plan

**Goal:** 운영자가 기존 도구를 유지하면서 AD 진단, UI 독립 공통 서비스, Nocturne 디자인을 사용한다.
**Architecture:** PowerShell 도구는 독립 실행한다. LDAP는 UI 독립 Shared 저장 서비스를 참조하고, WinForms/WPF는 네이티브 테마를 사용한다.
**Tech stack:** PowerShell 7, .NET 10, WinForms, WPF, xUnit, Pester. 추가 런타임 패키지 없음.

사용자 확정: Phase 1/2/4 권장안 진행, 스캐폴더(Phase 3)는 제외. Utilities 원본과 기존 graphify 산출물은 수정하지 않는다.

## 승인된 계약

- 종료 코드 0=성공, 1=실행 실패, 2=사용법. 실패 원인은 구조화된 FailureKind로 제공한다.
- PowerShell 자체 인자 바인딩 오류는 본문 이전 호스트 오류(종료 1)이며, 본문의 사용법 검증은 종료 2다. LDAP JSON의 분류 필드는 `failureCategory`다.
- LDAP 프리셋/최근 서버는 명시적 opt-in. 비밀번호, 사용자 ID, 사용자 속성, 원문 오류, 인증서 검증 생략은 저장하지 않는다. 기존 Utilities 데이터 자동 이전 없음.
- 인증서 검증 생략은 실행별 명시 옵션이며 LDAPS+LdapConnection에서만 허용. 사용자 속성 조회와 동시 사용 금지.
- Shared 저장 경로는 호출자 지정, 원자적 저장, 손상 파일 보존, 비동기 취소 전파. 최대 10개 최근 서버.
- 디자인은 기존 LDAP/MIP 기능과 화면 구조를 보존하고 OS 테마 및 고대비를 고려한다. HTML 토큰은 저장소 내부에 고정하고 외부 경로를 런타임에 참조하지 않는다.

## 작업과 소유권

- [x] Shared 담당: `src/OpsToolkit.Shared/` 6문서 → 저장/로그/보고서/진단 테스트 → BCL 구현. `tests/dotnet/OpsToolkit.Shared.Tests/` 검증. 저장 API 계약을 LDAP 담당과 먼저 공유.
- [x] LDAP 담당: `src/LdapAuthTester/` 명세 갱신 → CLI/서비스 검증 생략 및 실패 분류 테스트 → 실행별 옵션, opt-in 저장 UI → 기존/신규 xUnit. `NativeTheme.cs`는 디자인 담당 소유.
- [x] PowerShell 담당: `scripts/ad/Test-AdCredential/` 6문서 → `tests/powershell/Test-AdCredential.Tests.ps1` → 단일 Negotiate bind, WhatIf는 네트워크/암호 프롬프트 없음, TestConnectionOnly는 TCP만.
- [x] 디자인 담당: `DESIGN.md`, `design-system/`, LDAP `NativeTheme.cs`, MIP Explorer 네이티브 테마 및 문서. LDAP 기능 파일과 분리해 병렬 진행.
- [x] 통합 담당: `OpsToolkit.slnx`, 카탈로그, 루트 진행 문서, 빌드/포맷/.NET/Pester/분석 게이트 및 self-review 실행. 전체 포맷/기존 분석 부채와 대화형 GUI 미확인은 루트 REVIEW에 기록.

## 검증

1. `dotnet build -warnaserror` → 경고/오류 0.
2. `dotnet format --verify-no-changes` → 변경 요구 없음.
3. `dotnet test --nologo` → 전체 통과. 저장 손상 보존·취소, dry-run의 bind 금지, 인증서 옵션 조합을 포함한다.
4. `Invoke-Pester -Path ./tests/powershell/ -Output Detailed` → 전체 통과. `-WhatIf`와 로컬 TCP 리스너로 실제 AD 없이 검증한다.
5. `Invoke-ScriptAnalyzer -Path ./scripts/ -Recurse -Severity Warning,Error` → 기존 경고와 신규 경고를 구분한다.
6. GUI 캡처를 확인하고 초기 화면/레이아웃과 기능을 검사한다. 실제 AD 자격 증명 검증은 수행하지 않는다.
