# LdapAuthTester

> **한 줄 요약**: Active Directory(AD) 도메인 조인 상태 감지 및 LDAP(389) / LDAPS(636) 네트워크 연결성·사용자 인증을 안전하게 진단하는 하이브리드(WinForms GUI + CLI) 도구입니다.

---

## 1. 제작 취지 (Why this tool?)
사내 인프라나 서버에서 Active Directory(AD) 연동 오류가 발생할 때, 원인이 **방화벽/네트워크 포트(389, 636) 차단**인지, **LDAPS(SSL/TLS) 인증서 오류**인지, 아니면 **계정 비밀번호 불일치·계정 잠금·만료**인지 현장에서 즉시 판별하기 위해 제작되었습니다.

### 주요 특징
- **하이브리드 실행 (GUI + CLI)**:
  - 바탕화면이나 탐색기에서 실행(인자 없음)하면 직관적인 **WinForms GUI**가 열립니다.
  - 터미널이나 자동화 스크립트에서 옵션을 주어 실행하면 **CLI 모드(콘솔 및 `--json` 출력)**로 동작합니다.
- **AD 계정 잠금(Account Lockout) 방지**:
  - LDAP(389)과 LDAPS(636)를 연속으로 테스트할 때, 1차 시도에서 비밀번호 오류 등 자격 증명 실패가 발생하면 2차 시도를 자동으로 건너뛰어 **중복 실패로 인한 AD 계정 잠금**을 예방합니다.
- **사전 연결 점검 (`--dry-run`)**:
  - 실제 계정 인증(Bind)을 시도하지 않고 도메인 감지 및 대상 서버의 LDAP/LDAPS TCP 포트 개방 여부만 안전하게 점검할 수 있습니다.
- **3가지 표준 .NET 인증 방식 지원**:
  - `DirectoryEntry` (`System.DirectoryServices` NativeObject 바인딩)
  - `PrincipalContext` (`System.DirectoryServices.AccountManagement` ValidateCredentials)
  - `LdapConnection` (`System.DirectoryServices.Protocols` LDAP Bind)
- **상세 오류 코드 자동 해석**:
  - AD/LDAP 확장 에러 코드(`data 52e` 비밀번호 오류, `525` 사용자 없음, `775` 계정 잠금, `532` 비밀번호 만료, `533` 계정 비활성화, `701` 계정 만료, `773` 비밀번호 재설정 필요)와 Win32/SSL 오류를 한국어 진단 메시지로 즉시 변환합니다.

---

## 2. 실행 환경 및 사전 요구사항
- **OS**: Windows 10 / 11, Windows Server 2019 / 2022 / 2025
- **Runtime**: .NET 10 Desktop Runtime (`net10.0-windows`)
- **권한**: 일반 사용자 권한으로 실행 가능

---

## 3. 사용법 및 옵션 (Usage & Options)

### GUI 모드 실행 (Visual Studio 또는 CLI)
- **Visual Studio에서 단독 실행 (`F5`)**: `src/LdapAuthTester/LdapAuthTester.sln` 파일을 열면 `LdapAuthTester`(기본 시작 프로젝트)와 단위 테스트 프로젝트만 로드되어 즉시 `F5`로 실행 및 디버깅할 수 있습니다.
- **명령줄에서 GUI 실행**: 인자 없이 실행하거나 `--gui` 옵션을 전달하면 데스크톱 UI 창이 실행됩니다:
```powershell
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj
# 또는 빌드된 실행 파일 직접 실행
.\LdapAuthTester.exe
```

### CLI 모드 옵션

| 옵션 | 단축 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :---: | :--- |
| `--domain <host>` | `-d` | 조건부 필수 | 자동 감지 도메인 | 대상 AD 도메인 또는 DC 호스트명/IP |
| `--user <id>` | `-u` | 조건부 필수 | - | 테스트할 AD 사용자 계정 ID (`--dry-run`, `--env-only` 제외 시 필수) |
| `--password <pw>` | `-p` | 선택 | - | 사용자 비밀번호 (보안을 위해 `--password-env` 사용 권장) |
| `--password-env <var>` | - | 선택 | - | 비밀번호가 저장된 환경 변수 이름 (예: `LDAP_TEST_PASS`) |
| `--ldap-port <port>` | - | 선택 | `389` | LDAP 포트 번호 (`1`~`65535`) |
| `--ldaps-port <port>` | - | 선택 | `636` | LDAPS(SSL) 포트 번호 (`1`~`65535`) |
| `--target <target>` | `-t` | 선택 | `Both` | 점검 대상: `Both`, `LdapOnly` (`Ldap`), `LdapsOnly` (`Ldaps`) |
| `--method <method>` | `-m` | 선택 | `DirectoryEntry` | 검증 방식: `DirectoryEntry`, `PrincipalContext`, `LdapConnection` |
| `--timeout <sec>` | - | 선택 | `10` | 연결 및 인증 타임아웃(초) |
| `--no-lockout-guard` | - | 선택 | `false` | 자격 증명 실패 시에도 후속 프로토콜 강제 시도 |
| `--env-only` | - | 선택 | `false` | 현재 PC의 AD 도메인 조인 및 로그온 사용자 정보만 출력 |
| `--dry-run` | - | 선택 | `false` | 실제 인증 없이 도메인 감지 및 TCP 포트(389/636) 연결성만 점검 |
| `--json` | - | 선택 | `false` | 결과를 JSON 형식으로 출력 |
| `--verbose` | `-v` | 선택 | `false` | 상세 예외 스택 트레이스 포함 출력 |
| `--help` | `-h` | 선택 | - | 도움말 출력 |

---

## 4. 실행 예제 (Examples)

### 1) 현재 PC의 AD 도메인 조인 환경 조회 (`--env-only`)
```powershell
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj -- --env-only
```

### 2) Dry-Run: 계정 인증 없이 AD 서버 포트(389/636) 개방 여부만 안전하게 점검 (`--dry-run`)
```powershell
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj -- --domain dc01.corp.example.com --dry-run
```

### 3) 환경변수를 이용한 안전한 LDAP & LDAPS 인증 테스트 및 JSON 출력
```powershell
$env:AD_PASS = "MySecretPassword!"
dotnet run --project .\src\LdapAuthTester\LdapAuthTester.csproj -- --domain corp.example.com --user alice --password-env AD_PASS --method LdapConnection --json
Remove-Item Env:\AD_PASS
```
