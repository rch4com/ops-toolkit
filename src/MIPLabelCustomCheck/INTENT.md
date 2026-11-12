# INTENT.md — MIPLabelCustomCheck

## 1. 해결하려는 문제 (Problem Statement)
- 사내 문서 보안 및 DLP/DRM 운영 환경에서 Office(`.docx`, `.xlsx`, `.pptx`, `.doc`, `.xls`, `.ppt`), PDF, 이미지(`.png`, `.jpg`, `.tiff`), 이메일(`.msg`, `.eml`) 파일에 **Microsoft Purview (MIP, Microsoft Information Protection) 민감도 레이블(Sensitivity Label)**이 적용되어 있는지, 혹은 암호화(RMS/Native/PFile)로 보호되어 있는지 대량으로 신속하게 진단해야 할 때가 많습니다.
- 공식 MIP SDK를 사용하면 무거운 네이티브 C++ 런타임 의존성, 테넌트 인증 토큰, 네트워크 호출이 필요하여 폐쇄망 서버나 현장 진단 환경에서 즉시 실행하기 어렵고 대량 스캔 속도도 느립니다.
- 기존 별도 저장소(`rch4com/MIPLabelCustomCheck`)로 분리되어 있던 무의존성 메타데이터 파서 라이브러리와 WPF 탐색기 도구를 `ops-toolkit`(.NET 10 표준)으로 통합하고, GUI뿐 아니라 자동화 파이프라인에서 쓸 수 있는 CLI(`--path`, `--dry-run`, `--json`) 모드를 함께 제공하고자 합니다.

---

## 2. 기대 결과 및 성공 기준 (Expected Outcomes)
- **무의존성(Zero-Dependency) 고속 MIP 레이블 검출 코어 (`MIPLabelCustomCheck`, `net10.0`)**:
  - 외부 NuGet 패키지나 MIP SDK 없이 순수 .NET 10 기본 클래스 라이브러리(BCL)만으로 OOXML, OLE CFB(구형 Office 및 Outlook MSG), PDF, PNG, JPEG, TIFF, EML, `.pfile`의 레이블 GUID 및 상세 메타데이터(`SiteId`, `Name`, `Method`, `ContentBits`, `SetDate`), 보호 상태(`None`, `Native`, `GenericPFile`)를 밀리초(ms) 단위로 판별합니다.
- **WPF Explorer GUI + CLI 하이브리드 실행 (`MIPLabelCustomCheck.Explorer`, `net10.0-windows`)**:
  - 인자 없이 실행 시 폴더 트리·파일 목록·소요 시간·상세 메타데이터를 실시간 병렬 스캔하는 **WPF 탐색기 GUI**가 실행됩니다.
  - CLI 인자(`--path`, `--recursive`, `--details`, `--dry-run`, `--json`) 전달 시 창을 띄우지 않고 콘솔 또는 JSON 포맷으로 결과를 출력하고 표준 종료 코드(`0`, `1`, `2`)를 반환합니다.
- **2-Tier 솔루션 및 전체 테스트/벤치마크 통합**:
  - `src/MIPLabelCustomCheck/MIPLabelCustomCheck.sln`을 열면 Visual Studio에서 즉시 Explorer 앱을 `F5`로 실행할 수 있으며, 코어/Explorer 단위 테스트(`tests/dotnet/`)와 `BenchmarkDotNet` 성능 측정 프로젝트까지 모두 .NET 10에서 경고 없이 빌드 및 통과합니다.

---

## 3. Non-Goals (하지 않을 것 / 안전 제약)
- **파일 수정 절대 금지 (Read-Only Guarantee)**: 이 도구는 대상 문서나 이미지 파일을 오직 읽기 전용(`FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete`)으로만 열며, 어떠한 경우에도 원본 파일의 내용이나 메타데이터를 변경하지 않습니다.
- **MIP SDK / 외부 네이티브 DLL 도입 금지**: 코어 라이브러리(`MIPLabelCustomCheck`)는 외부 NuGet 패키지 의존성 `0`개를 엄격히 유지합니다.
- **암호화 본문 복호화 시도 금지**: 암호화된 문서 본문을 복호화하거나 인증 키를 요구하지 않으며, 파일 구조상 읽을 수 있는 레이블/보호 마커만 안전하게 판별합니다.
