# REVIEW.md — LdapAuthTester

## 1. 품질 및 보안 체크리스트
- [x] **보안 (Secrets 미포함)**: 사내 도메인, IP, 계정, 비밀번호, 고객사명(`Feelanet`, `SCBank`, `DongSuh` 등) 및 사내 라이브러리(`art.CNR.Common`) 참조가 모두 제거되었는가?
- [x] **안전성 (`--dry-run` & 계정 잠금 방지)**:
  - `--dry-run` 실행 시 실제 AD 자격 증명 바인딩을 수행하지 않고 TCP 포트 연결성만 안전하게 점검하는가?
  - 1차 프로토콜 자격 증명 실패 시 2차 프로토콜 시도를 자동 중단하여 계정 잠금을 방지하는가?
- [x] **사용성 (`README.md`)**: 다른 개발자/운영자가 `README.md`만 읽고도 GUI 실행 방법과 CLI 옵션, 예제를 명확히 이해할 수 있는가?
- [x] **코드 품질 및 테스트**:
  - `dotnet build -warnaserror` 경고/오류 0건 통과
  - `dotnet format --verify-no-changes` 통과
  - `dotnet test --nologo` 단위 테스트 15건 전체 통과
  - 루트 `README.md` 유틸리티 카탈로그 등록 완료

---

## 2. 리뷰 기록 요약

### [2026-11-13] LdapAuthTester 퍼블릭화 및 .NET 10 이관 리뷰 결과
- **상태**: PASS
- **검증 명령어 결과**:
  - Build (`dotnet build -warnaserror`): Pass (경고 0, 오류 0)
  - Format (`dotnet format --verify-no-changes`): Pass
  - Tests (`dotnet test --nologo`): Pass (15/15 통과)
  - DryRun Check (`--dry-run` 및 `--env-only`): Pass
- **주요 확인 사항**:
  - 사내 전용 네임스페이스(`art.CNR.*`), 사내 DLL(`art.CNR.Common`), 특정 고객사 빌드 구성 전수 제거 완료
  - 3번째 인증 방식을 표준 `System.DirectoryServices.Protocols.LdapConnection` 바인딩으로 교체 완료
  - WinForms GUI + CLI 하이브리드 진입점 및 `--password-env`, `--dry-run`, `--json` 지원 완료
