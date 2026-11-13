# REVIEW.md — MIPLabelCustomCheck

## Nocturne 네이티브 디자인 검증 (2026-11-13)

- [x] SPEC/PLAN 및 루트 DESIGN.md의 승인된 범위를 코드보다 먼저 기록
- [x] 공유 `Themes/Nocturne.xaml`, OS 앱 테마 반영, 고대비 시스템 색상 매핑, 의미 리소스 기반 상태·오류·선택·포커스·비활성 표시 적용
- [x] Explorer 경고 오류 처리 빌드: 경고 0, 오류 0
- [x] 기존 Explorer 테스트 91/91, `design-system/tests/palette.test.cjs` 대비 검사 3/3 통과
- [x] 변경 C# 대상 `dotnet format --verify-no-changes --no-restore --include ...` 통과
- [x] STA RenderTargetBitmap으로 합성 데이터의 1200×660 본문을 강제 라이트/다크 팔레트, 96 DPI에서 캡처·검토. 다크 폴더명 대비를 수정하고 재확인
- [ ] 실제 창의 대화형 키보드·스크린리더·고대비·실행 중 OS 테마 변경·다양한 DPI 검증: 미확인

Computer Use의 `launch_app`이 `accessibility window-opened handler did not become ready`로 실패하여 실제 창을 열지 못했다. 캡처는 앱 리소스와 실제 MainWindow XAML을 사용한 렌더 하네스이며, 실제 대화형 검증을 대체하지 않는다. 드롭다운·표 헤더·스크롤바 등 네이티브 템플릿은 OS 렌더링을 유지한다. HTML/CSS 디자인 감지기는 네이티브 UI에 적용하지 않았다. 인증·실제 문서 스캔은 디자인 검증에서 실행하지 않았다.

캡처: `artifacts/nocturne-review/mip-forced-light-96dpi.png`, `mip-forced-dark-96dpi.png` (로컬 검증 산출물, 저장소 추적 제외).

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
