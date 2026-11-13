# AGENTS.md — LdapAuthTester

이 도구는 루트 [AGENTS.md](../../AGENTS.md)의 규칙을 따릅니다.
코드를 수정하거나 기능을 추가하기 전에 현재 폴더의 [INTENT.md](./INTENT.md), [SPEC.md](./SPEC.md), [PLAN.md](./PLAN.md), [REVIEW.md](./REVIEW.md)를 먼저 확인하세요.

---

## 로컬 빌드, 실행 및 테스트 명령어

```powershell
# 1. 도구 전용 솔루션(LdapAuthTester.sln) 빌드 및 테스트 (Visual Studio 단독 오픈 가능)
dotnet build .\src\LdapAuthTester\LdapAuthTester.sln -warnaserror
dotnet test .\src\LdapAuthTester\LdapAuthTester.sln --nologo

# 2. 코드 포맷 검증
dotnet format .\src\LdapAuthTester\LdapAuthTester.sln --verify-no-changes

# 3. CLI 도움말 및 환경 감지 실행
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj -- --help
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj -- --env-only

# 4. Dry-Run 검증 (실제 AD 인증 없이 TCP 연결성 및 LDAPS TLS 인증서 메타데이터를 점검)
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj -- --domain localhost --dry-run --timeout 2

# 5. 단위 테스트 실행
dotnet test .\tests\dotnet\LdapAuthTester.Tests\LdapAuthTester.Tests.csproj --nologo
```

---

## 도구별 특수 주의사항 (Domain Rules)
1. **사내 종속성 추가 금지**: 이 도구는 퍼블릭/범용 유틸리티로 설계되었습니다. 특정 사내 라이브러리나 하드코딩된 사내 도메인/IP를 코드에 추가하지 마세요.
2. **AD 계정 잠금(Account Lockout) 방지 유지**: `SkipSecondOnCredentialFailure` 기본값은 항상 `true`여야 하며, 1차 프로토콜에서 자격 증명 실패가 확인되면 2차 프로토콜 바인딩을 건너뛰어야 합니다.
3. **하이브리드 진입점 호환성**: 인자 없이 실행할 때는 WinForms GUI(`MainForm`)가 뜨고, CLI 인자가 주어졌을 때는 GUI 창을 띄우지 않고 콘솔 표준 출력/표준 에러와 종료 코드(`0`, `1`, `2`)만 반환해야 합니다.
