# SPEC.md — LdapAuthTester

## 1. 프로젝트 및 런타임 사양
- **도구 경로**: `src/LdapAuthTester/LdapAuthTester.csproj`
- **테스트 경로**: `tests/dotnet/LdapAuthTester.Tests/LdapAuthTester.Tests.csproj`
- **타겟 프레임워크**: `net10.0-windows` (`UseWindowsForms=true`, `OutputType=Exe`)
- **외부 패키지 의존성**:
  - `System.DirectoryServices` (Windows Desktop Shared Framework 기본 포함)
  - `System.DirectoryServices.AccountManagement`
  - `System.DirectoryServices.Protocols`

---

## 2. 실행 모드 및 CLI 옵션 명세 (Input Contract)

- **GUI 모드**: 명령줄 인자 없이 실행하거나 `--gui` 옵션을 주면 WinForms GUI(`MainForm`)를 실행합니다.
- **CLI 모드**: `--gui` 외의 명령줄 인자가 1개 이상 전달되면 콘솔 모드로 동작합니다.

| CLI 옵션 | 단축 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :---: | :--- |
| `--domain <host>` | `-d` | 조건부 필수 | 자동 감지 도메인 | 대상 AD 도메인 이름 또는 Domain Controller 호스트/IP (`--env-only`가 아닐 때 미지정 시 자동 감지된 도메인 사용) |
| `--user <id>` | `-u` | 조건부 필수 | - | 인증할 AD 사용자 ID (`--dry-run` 또는 `--env-only`가 아닐 때 필수) |
| `--password <pw>` | `-p` | 선택 | - | 사용자 비밀번호 (`--password-env`로 대체 가능, `--dry-run` 시 불필요) |
| `--password-env <var>` | - | 선택 | - | 비밀번호를 읽어올 환경 변수 이름 (명령줄 히스토리 노출 방지 권장 옵션) |
| `--ldap-port <port>` | - | 선택 | `389` | LDAP 포트 (`1` ~ `65535`) |
| `--ldaps-port <port>` | - | 선택 | `636` | LDAPS(SSL) 포트 (`1` ~ `65535`) |
| `--target <target>` | `-t` | 선택 | `Both` | 테스트 대상 프로토콜: `Both`, `LdapOnly` (`Ldap`), `LdapsOnly` (`Ldaps`) |
| `--method <method>` | `-m` | 선택 | `DirectoryEntry` | 검증 방식: `DirectoryEntry`, `PrincipalContext`, `LdapConnection` |
| `--timeout <sec>` | - | 선택 | `10` | 연결 및 바인딩 타임아웃(초, `1` ~ `300`) |
| `--no-lockout-guard` | - | 선택 | `false` | 설정 시 첫 번째 프로토콜 자격 증명 실패 후에도 후속 프로토콜 테스트 강제 진행 (기본값은 중단하여 계정 잠금 방지) |
| `--include-user-info` | `-i` | 선택 | `false` | 인증 성공 시 사용자의 AD 속성(`distinguishedName`, `displayName`, `mail`, `department`, `title`) 및 `memberOf` 그룹 목록 추가 조회 |
| `--env-only` | - | 선택 | `false` | 현재 PC의 Windows 로그인 사용자 및 AD 도메인 조인 상태만 조회하고 종료 |
| `--dry-run` | - | 선택 | `false` | 실제 AD 자격 증명 인증(Bind)을 수행하지 않고, 도메인 환경 감지 및 대상 포트(389/636) TCP 연결성만 사전 점검 |
| `--json` | - | 선택 | `false` | 결과를 구조화된 JSON 포맷으로 표준 출력(`stdout`)에 출력 |
| `--verbose` | `-v` | 선택 | `false` | 원본 예외 메시지 등 상세 진단 정보 포함 출력 |
| `--help` | `-h` | 선택 | - | 사용법 및 예제 출력 |
| `--version` | - | 선택 | - | 버전 정보 출력 |

---

## 3. 출력 데이터 및 종료 코드 (Output Contract)

### 3.1 종료 코드 (Exit Code)
- `0`: 성공 (모든 대상 프로토콜 인증 성공, `--dry-run` 시 모든 대상 포트 TCP 연결 성공, 또는 `--env-only` / `--help` / `--version` 정상 출력)
- `1`: 런타임 또는 검증 실패 (인증 실패, 포트 연결 실패, 예외 발생)
- `2`: 잘못된 CLI 인자/옵션 입력 (필수 인자 누락, 포트 범위 오류, 알 수 없는 옵션 등)

### 3.2 JSON 출력 스키마 (`--json`)
```json
{
  "timestamp": "2026-11-13T00:50:00.0000000+09:00",
  "isDryRun": false,
  "environment": {
    "isDomainJoined": true,
    "domainName": "corp.example.com",
    "domainController": "dc01.corp.example.com",
    "currentLogonUser": "CORP\\alice",
    "currentNetBiosUser": "CORP\\alice",
    "detectionMessage": "AD 도메인에 조인됨: corp.example.com"
  },
  "overallSuccess": true,
  "results": [
    {
      "protocol": "LDAP",
      "endpoint": "LDAP://corp.example.com:389",
      "isSuccess": true,
      "isDryRun": false,
      "isSkipped": false,
      "skipReason": null,
      "isCredentialFailure": false,
      "isNetworkOrServerFailure": false,
      "win32ErrorCode": 0,
      "ldapDataCode": null,
      "diagnosticDetail": "LDAP (Port 389) 인증 성공 (42 ms)",
      "elapsedMilliseconds": 42
    }
  ]
}
```

---

## 4. 핵심 동작 로직 및 안전장치 명세

### 4.1 3가지 표준 인증 검증 방식 (`AuthTestMethod`)
1. **`DirectoryEntry`**:
   - `LDAP://{Domain}:{Port}` 경로로 `System.DirectoryServices.DirectoryEntry`를 생성하고 `NativeObject` 바인딩을 수행합니다.
   - `LDAPS`인 경우 `AuthenticationTypes.SecureSocketsLayer | AuthenticationTypes.Secure`를 적용합니다.
2. **`PrincipalContext`**:
   - `System.DirectoryServices.AccountManagement.PrincipalContext(ContextType.Domain, "{Domain}:{Port}", null, options)`를 생성하고 `ValidateCredentials(UserName, Password, options)`를 호출합니다.
3. **`LdapConnection`**:
   - `System.DirectoryServices.Protocols.LdapConnection(new LdapDirectoryIdentifier(Domain, Port))`을 생성하고 `NetworkCredential`로 `Bind()`를 수행합니다.
   - `LDAPS`인 경우 `SessionOptions.SecureSocketLayer = true`를 설정합니다.

### 4.2 `--dry-run` (사전 연결 점검) 동작
- `--dry-run` 플래그(또는 GUI의 `사전 연결 점검 (Dry-Run)` 버튼)가 활성화되면:
  - `UserName`과 `Password`가 없어도 실행 가능하며, 실제 AD 자격 증명 바인딩을 절대 호출하지 않습니다(계정 잠금 위험 0%).
  - 대상 프로토콜(`LDAP`, `LDAPS`, 또는 `Both`)의 호스트와 포트로 `TcpClient` 비동기 연결(`ConnectAsync` + `CancellationToken` 타임아웃)을 시도하여 네트워크/방화벽 개방 여부를 확인하고 결과를 반환합니다.

### 4.3 계정 잠금 방지 (`SkipSecondOnCredentialFailure`)
- `TestTarget == Both`일 때 1차 프로토콜(LDAP)에서 자격 증명 오류(`IsCredentialFailure == true`: LDAP data code `52e`, `525`, `775`, `532`, `533`, `701`, `773` 또는 Win32 `0x8007052E` / `LdapException` invalid credentials `49`)가 발생하면, 2차 프로토콜(LDAPS)은 바인딩을 시도하지 않고 `IsSkipped = true`로 기록합니다.

## 5. 승인된 Phase 1 계약
- `--skip-certificate-validation`: 기본 false, 현재 실행에만 적용. `--target LdapsOnly --method LdapConnection` 조합만 허용하며 `--include-user-info`와 결합 시 Usage Error(2). 서비스 직접 호출도 동일하게 검증합니다.
- 종료 코드 0/1/2 유지. 결과 `failureCategory`는 None, Credential, NetworkOrServer, Tls, Timeout, Unknown, Skipped로 실패를 구조화합니다. 우회 여부는 `certificateValidationBypassed`로 명시합니다.
- Dry-run은 Bind/사용자 조회를 하지 않습니다. TCP 연결을 점검하며 LDAPS는 별도 TLS 핸드셰이크로 인증서 메타데이터를 수집합니다. 이 진단 핸드셰이크는 신뢰 오류가 있는 인증서도 수집하며 TCP 성공은 인증/인증서 신뢰 성공을 뜻하지 않습니다.
- GUI 로컬 저장 기본 OFF. 사용자가 선택한 세션에서만 `%LOCALAPPDATA%/LdapAuthTester`의 presets.json / history.json을 읽고 씁니다. 서버 설정 프리셋 저장/불러오기/삭제 및 최근 서버 최대 10개 중복 제거/명시적 지우기를 제공합니다.
- 저장 DTO 허용 목록: Domain, LdapPort, LdapsPort, TestMethod, TestTarget, TimeoutSeconds. 사용자 ID/비밀번호/프로필/원본 오류/검증 우회는 저장 금지. 알 수 없는 저장 필드/잘못된 JSON은 명시적으로 실패하고 원본 파일을 덮어쓰지 않습니다. Utilities 자동 이관 없음.
