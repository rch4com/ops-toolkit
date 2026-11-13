# REVIEW.md — MIPLabelCustomCheck

## 1. 품질 및 보안 체크리스트
- [x] **보안 (Secrets & XXE/DoS 방어)**:
  - 코드 및 샘플 파일에 사내 비밀번호, 개인정보, 민감 서버 정보가 포함되어 있지 않은가?
  - XML 파싱 시 DTD 금지(`SafeXml`) 및 스트림 읽기 한도(`LimitedReadStream`)가 유지되고 있는가?
- [x] **안전성 (Read-Only & `--dry-run`)**:
  - 모든 파일 접근이 읽기 전용(`FileAccess.Read`)이며 `--dry-run` 실행 시 파일을 열지 않고 대상 목록만 안전하게 출력하는가?
- [x] **사용성 (`README.md` & 2-Tier `.sln`)**:
  - `src/MIPLabelCustomCheck/MIPLabelCustomCheck.sln`으로 단독 실행/빌드가 가능하며 `README.md`에 GUI/CLI/라이브러리 사용법이 명확히 문서화되어 있는가?
- [x] **코드 품질 및 테스트**:
  - `dotnet build -warnaserror` 경고/오류 0건 통과
  - `dotnet test --nologo` 코어 및 Explorer 단위 테스트 전체 통과
  - 루트 `README.md` 카탈로그 등록 및 기존 `MIPLabelCustomCheck` 저장소 이전 공지 완료

---

## 2. 리뷰 기록 요약

### [2026-11-13] MIPLabelCustomCheck .NET 10 이관 및 CLI 하이브리드 확장 리뷰 결과
- **상태**: PASS
- **검증 명령어 결과**:
  - Build (`dotnet build -warnaserror`): Pass (경고 0, 오류 0)
  - Tests (`dotnet test --nologo`): Pass
  - DryRun & CLI Check (`--path ... --dry-run` / `--details`): Pass
- **주요 확인 사항**:
  - 코어 라이브러리(`MIPLabelCustomCheck`) 외부 NuGet 의존성 0개 유지 완료
  - `MIPLabelCustomCheck.Explorer`에 WPF GUI + CLI(`--path`, `--recursive`, `--details`, `--dry-run`, `--json`, `--output-csv`, `--output-json`) 하이브리드 모드 추가 완료
  - WPF Explorer GUI에 `.msg`/`.eml` 기본 스캔 확장자 포함, 하위 폴더 재귀 스캔(`IncludeSubfolders`), 상태 필터(`SelectedStatusFilter`), UTF-8(BOM) CSV 내보내기(`ExportCsvCommand`) 추가 완료
  - `MIPLabelCustomCheck.sln`(개별 실행용) 및 루트 `OpsToolkit.slnx` 2-Tier 솔루션 연동 완료
