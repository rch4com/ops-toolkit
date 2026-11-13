# REVIEW.md — LdapAuthTester

## 1. 품질 및 보안 체크리스트
- [x] **보안 (Secrets 미포함)**: 사내 도메인, IP, 계정, 비밀번호, 고객사명(`Feelanet`, `SCBank`, `DongSuh` 등) 및 사내 라이브러리(`art.CNR.Common`) 참조가 모두 제거되었는가?
- [x] **안전성 (`--dry-run` & 계정 잠금 방지)**:
  - `--dry-run` 실행 시 실제 AD 자격 증명 바인딩을 수행하지 않고 TCP 연결성 및 LDAPS TLS 인증서 메타데이터를 안전하게 점검하는가?
  - 1차 프로토콜 자격 증명 실패 시 2차 프로토콜 시도를 자동 중단하여 계정 잠금을 방지하는가?
- [x] **사용성 (`README.md`)**: 다른 개발자/운영자가 `README.md`만 읽고도 GUI 실행 방법과 CLI 옵션, 예제를 명확히 이해할 수 있는가?
- [x] **코드 품질 및 테스트**:
  - `dotnet build -warnaserror` 경고/오류 0건 통과
  - `dotnet format --verify-no-changes` 통과
  - `dotnet test --nologo` 단위 테스트 19건 전체 통과
  - 루트 `README.md` 유틸리티 카탈로그 등록 완료

---

## 2. 리뷰 기록 요약

### [2026-11-13] LdapAuthTester 퍼블릭화, .NET 10 이관 및 LDAPS 인증서/프로필 조회 고도화 리뷰 결과
- **상태**: PASS
- **검증 명령어 결과**:
  - Build (`dotnet build -warnaserror`): Pass (경고 0, 오류 0)
  - Format (`dotnet format --verify-no-changes`): Pass
  - Tests (`dotnet test --nologo`): Pass (19/19 통과)
  - DryRun Check (`--dry-run` 및 `--env-only`): Pass
- **주요 확인 사항**:
  - 사내 전용 네임스페이스(`art.CNR.*`), 사내 DLL(`art.CNR.Common`), 특정 고객사 빌드 구성 전수 제거 완료
  - 3번째 인증 방식을 표준 `System.DirectoryServices.Protocols.LdapConnection` 바인딩으로 교체 완료
  - WinForms GUI + CLI 하이브리드 진입점 및 `--password-env`, `--dry-run`, `--json` 지원 완료
  - LDAPS(636) SSL/TLS 원격 인증서(Subject, Issuer, 만료일, SslPolicyErrors, 체인 오류) 자동 진단 및 `--include-user-info`(`-i`) 사용자 AD 속성/그룹 조회(LDAP 필터 이스케이프 포함) 구현 완료

### [2026-11-13] Phase 1 실행별 TLS 진단 / 선택적 로컬 서버 저장
- **상태**: PASS (로컬 자동화 검증), 실제 AD 검증 제외
- **검증**: 도구 솔루션 build `-warnaserror` 경고/오류 0; xUnit 36/36; 도구 솔루션 format 적용 및 검사.
- **보안**: 기본 OFF 서버 저장, 저장 DTO 허용 목록/알 수 없는 필드 거부, 재로드/삭제/중복제거 최대10개/명시적 비우기/손상 파일 비덮어쓰기 테스트 통과. 비밀번호·계정 ID·사용자 프로필·원본 오류·우회 설정 저장 없음. Utilities 자동 이관 없음.
- **TLS**: CLI 파서 및 서비스 직접 호출에서 LDAPS 단독 + LdapConnection만 우회 허용, 사용자 조회 결합 거부. 명시적 경고/결과 우회 표시, GUI 실행 후 초기화. 0/1/2 유지 및 구조화 실패 분류.
- **안전성**: 기존 자격 증명 오류 후 후속 바인딩 생략 유지. Dry-run Bind 미호출 테스트 및 기존 localhost TCP/TLS 인증서 테스트 통과. 메타데이터 TLS 핸드셰이크를 문서에 명시.
- **종료 안전성**: 폼 종료 시 취소 후 실행/저장 작업이 끝난 다음 저장 잠금과 CTS를 해제합니다. 종료 뒤 UI 콜백/대화상자를 차단하며 STA 비동기 종료 회귀 테스트 통과.
- **제한**: 실제 AD 인증/계정 잠금 정책 검증은 수행하지 않음. 이미 시작된 동기 Windows 바인딩은 취소 즉시 중단을 보장하지 않음. GUI 시각 검증은 별도 디자인 담당 결과 참조.
