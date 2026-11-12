# INTENT.md — LdapAuthTester

## 1. 해결하려는 문제 (Problem Statement)
- 기업 인프라 및 애플리케이션 운영 환경에서 Active Directory(AD) 연동 장애가 발생했을 때, 문제 원인이 **네트워크/방화벽 포트(389/636) 차단**인지, **LDAPS(SSL/TLS) 인증서 신뢰 문제**인지, 아니면 **사용자 자격 증명(비밀번호 오류, 계정 잠금, 만료 등)** 문제인지 현장에서 즉시 구분하기 어렵습니다.
- 기존 사내 테스트 도구(`art.CNR.LdapAuthTest`)는 `.NET Framework 4.7.2` 기반에 사내 전용 라이브러리(`art.CNR.Common`) 및 특정 고객사 빌드 구성에 종속되어 있어, 범용 운영 유틸리티 저장소(`ops-toolkit`)에서 독립적으로 빌드하거나 배포하기 어려웠습니다.
- 또한, 잘못된 비밀번호로 LDAP(389)과 LDAPS(636)를 연속 테스트할 경우 실패 횟수가 중복 누적되어 **AD 계정 잠금(Account Lockout) 정책**을 유발할 위험이 있으며, GUI 환경뿐 아니라 원격 터미널/자동화 파이프라인(CLI)에서도 동일하게 사용할 수 있는 도구가 필요합니다.

---

## 2. 기대 결과 및 성공 기준 (Expected Outcomes)
- **완전한 퍼블릭/독립형 구조 (Zero Internal Dependency)**:
  - 사내 전용 네임스페이스(`art.CNR.*`), 사내 DLL(`art.CNR.Common`), 고객사별 빌드 설정을 모두 제거하고 표준 .NET 10 (`net10.0-windows`) 및 공식 NuGet 패키지(`System.DirectoryServices`, `System.DirectoryServices.AccountManagement`, `System.DirectoryServices.Protocols`)만으로 동작합니다.
- **하이브리드 실행 모드 지원 (WinForms GUI + CLI)**:
  - 인자 없이 실행하면 현장 엔지니어가 직관적으로 사용할 수 있는 **WinForms GUI**가 실행됩니다.
  - CLI 인자(`--domain`, `--user`, `--dry-run`, `--json` 등)를 전달하면 **콘솔/JSON 출력 및 표준 종료 코드(`0`, `1`, `2`)**를 반환하는 CLI 모드로 동작합니다.
- **3가지 표준 .NET AD 검증 방식 지원**:
  1. `DirectoryEntry` (`System.DirectoryServices` NativeObject 바인딩)
  2. `PrincipalContext` (`System.DirectoryServices.AccountManagement` `ValidateCredentials`)
  3. `LdapConnection` (`System.DirectoryServices.Protocols` `Bind` — 기존 사내 `CredentialManager` 대체)
- **계정 잠금(Account Lockout) 방지 및 `--dry-run` 안전장치**:
  - `--dry-run` (및 GUI의 '사전 연결 점검(Dry-Run)') 실행 시 실제 AD 자격 증명 바인딩을 생략하고, 현재 PC의 도메인 조인 상태 및 대상 엔드포인트(LDAP 389 / LDAPS 636)의 **TCP 포트 연결 가능 여부만 안전하게 점검**합니다.
  - 실제 인증 테스트 시 첫 번째 프로토콜(LDAP)에서 자격 증명 오류(`52e`, `775` 등)가 감지되면 후속 프로토콜(LDAPS) 시도를 즉시 중단(`SkipSecondOnCredentialFailure`)하여 중복 실패로 인한 계정 잠금을 방지합니다.

---

## 3. Non-Goals (하지 않을 것 / 안전 제약)
- **비밀번호/민감정보 저장 금지**: 입력된 비밀번호나 사내 도메인/DC 정보를 설정 파일이나 로그에 평문으로 기록하거나 저장하지 않습니다.
- **AD 객체 상태 변경 금지**: 이 도구는 오직 읽기/바인딩을 통한 인증 및 연결성 진단만 수행하며, AD 사용자 비밀번호 변경이나 계정 잠금 해제 등 디렉터리 상태를 수정하는 작업은 수행하지 않습니다.
- **사내 전용 라이브러리 재도입 금지**: 외부 환경이나 다른 팀에서도 즉시 빌드/사용할 수 있도록 사내 프라이빗 패키지 참조를 추가하지 않습니다.
