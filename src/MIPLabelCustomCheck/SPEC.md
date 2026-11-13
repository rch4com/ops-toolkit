# SPEC.md — MIPLabelCustomCheck

## 1. 컴포넌트 및 프로젝트 구조
- **도구 루트 폴더**: `src/MIPLabelCustomCheck/`
- **개별 실행용 솔루션**: `src/MIPLabelCustomCheck/MIPLabelCustomCheck.sln`
- **하위 프로젝트 구성**:
  1. `src/MIPLabelCustomCheck/MIPLabelCustomCheck/MIPLabelCustomCheck.csproj` (`net10.0` 클래스 라이브러리, 외부 NuGet 의존성 0개)
  2. `src/MIPLabelCustomCheck/MIPLabelCustomCheck.Explorer/MIPLabelCustomCheck.Explorer.csproj` (`net10.0-windows`, `OutputType=Exe`, `UseWPF=true` — WPF GUI + CLI 하이브리드 실행 파일)
  3. `src/MIPLabelCustomCheck/MIPLabelCustomCheck.Benchmarks/MIPLabelCustomCheck.Benchmarks.csproj` (`net10.0`, `BenchmarkDotNet` 성능 벤치마크)
- **단위 테스트 프로젝트**:
  1. `tests/dotnet/MIPLabelCustomCheck.Tests/MIPLabelCustomCheck.Tests.csproj` (`net10.0`, 코어 파서 및 보안 한계 단위 테스트)
  2. `tests/dotnet/MIPLabelCustomCheck.Explorer.Tests/MIPLabelCustomCheck.Explorer.Tests.csproj` (`net10.0-windows`, WPF ViewModel, 스캔 세션, CLI 러너 단위 테스트)

---

## 2. 실행 모드 및 CLI 옵션 명세 (`MIPLabelCustomCheck.Explorer`)

- **GUI 모드**: 명령줄 인자 없이 실행하거나 `--gui` 옵션을 주면 WPF 탐색기(`MainWindow`)가 실행됩니다.
  - 하위 폴더 재귀 스캔(`IncludeSubfolders` / `"하위 폴더 포함"` 체크박스) 지원
  - 상태 필터(`SelectedStatusFilter`: `"전체"`, `"레이블됨"`, `"미레이블"`, `"보호됨"`, `"오류/잘못된파일"`, `"대상 외"`)를 통한 실시간 목록 필터링(`FilteredFiles`) 지원
  - 스캔된 파일 목록을 UTF-8(BOM) CSV 파일로 내보내는 `"CSV 내보내기"`(`ExportCsvCommand`) 지원
- **CLI 모드**: `--gui` 외의 인자가 1개 이상 전달되면 콘솔 모드로 동작합니다.

| CLI 옵션 | 단축 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :---: | :--- |
| `--path <path>` | `-p` | 조건부 필수 | - | 검사할 단일 파일 또는 디렉터리 경로 (`--help`, `--version` 외 필수) |
| `--recursive` | `-r` | 선택 | `false` | `--path`가 디렉터리일 때 하위 폴더까지 재귀 탐색 |
| `--all-files` | `-a` | 선택 | `false` | 디렉터리 스캔 시 알려진 레이블 가능 확장자 외의 모든 파일도 검사 |
| `--details` | `-d` | 선택 | `false` | 레이블 GUID 외에 상세 메타데이터(`SiteId`, `Name`, `Method`, `ContentBits`, `SetDate`)까지 파싱 (`LabelCheckOptions.WithDetails`) |
| `--dry-run` | - | 선택 | `false` | 실제 파일 스트림을 열어 파싱하지 않고, 검사 대상 파일 목록과 확장자/크기만 사전 확인 |
| `--json` | - | 선택 | `false` | 검사 결과를 JSON 포맷으로 표준 출력(`stdout`)에 출력 |
| `--output-csv <file>` | - | 선택 | - | 검사 결과를 UTF-8(BOM) CSV 파일로 저장 |
| `--output-json <file>` | - | 선택 | - | 검사 결과를 JSON 파일로 저장 |
| `--verbose` | `-v` | 선택 | `false` | 개별 파일 소요 시간(ms) 및 보호 상세(`ProtectionDetail`) 등 추가 정보 출력 |
| `--gui` | - | 선택 | `false` | WPF Explorer GUI 명시적 실행 |
| `--help` | `-h` | 선택 | - | 도움말 및 사용 예제 출력 |
| `--version` | - | 선택 | - | 버전 출력 |

---

## 3. 출력 계약 및 종료 코드 (Output Contract)

### 3.1 종료 코드 (Exit Code)
- `0`: 성공 (대상 파일/폴더 스캔이 정상 완료됨, 또는 `--dry-run` / `--help` / `--version` 성공)
- `1`: 런타임 오류 (지정한 파일/경로가 존재하지 않거나, 스캔 중 파일 잠금/읽기 오류 발생)
- `2`: 잘못된 CLI 인자/옵션 입력 (필수 `--path` 누락, 알 수 없는 옵션 등)

### 3.2 지원 파일 포맷 및 판별 상태 (`LabelStatus` & `ProtectionType`)
- **지원 포맷**:
  - OOXML (`.docx`, `.docm`, `.dotx`, `.dotm`, `.xlsx`, `.xlsm`, `.xltx`, `.xltm`, `.pptx`, `.pptm`, `.potx`, `.potm`, `.ppsx`, `.ppsm`)
  - Legacy Office CFB (`.doc`, `.dot`, `.xls`, `.xlt`, `.ppt`, `.pot`, `.pps`)
  - PDF (`.pdf`), PNG (`.png`), JPEG (`.jpg`, `.jpeg`), TIFF (`.tif`, `.tiff`)
  - Outlook MSG (`.msg`), EML (`.eml`), Generic PFile (`.pfile`)
- **`LabelStatus`**:
  - `Labeled`: 활성화된 MIP 민감도 레이블 GUID가 1개 이상 검출됨
  - `Unlabeled`: 지원되는 포맷이며 정상 파싱되었으나 적용된 레이블이 없음
  - `ProtectedLabelUnknown`: 암호화/RMS 보호로 인해 레이블 메타데이터 영역을 읽을 수 없음 (`Protection` 속성 참조)
  - `Unsupported`: 지원하지 않는 파일 형식
  - `InvalidFile`: 손상되었거나 규격에 맞지 않는 파일 구조
- **`ProtectionType`**:
  - `None`: 암호화/보호 미감지
  - `Native`: 포맷 고유 암호화 감지 (Office IRM/DataSpaces/EncryptedPackage 또는 PDF `/Encrypt`)
  - `GenericPFile`: `.pfile` 래퍼 암호화 감지

---

## 4. 안전장치 (`--dry-run` 및 보안 제한)
- **읽기 전용 및 공유 모드**: 모든 파일은 `FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete`로만 열어 다른 프로세스의 작업을 방해하지 않습니다.
- **악성/거대 파일 방어 (Security Limits)**:
  - ZIP 폭탄, 순환 참조 PDF XRef, 비정상 엔트리 크기에 대해 스트림 읽기 상한(`LimitedReadStream`)과 엔트리 크기 제한을 적용하여 메모리 고갈(OOM)을 방지합니다.
  - XML 파싱 시 DTD 처리 비활성화(`DtdProcessing.Prohibit`) 및 `XmlResolver = null`(`SafeXml`)을 강제하여 XXE 공격을 차단합니다.
- **`--dry-run` 동작**:
  - 실제 파일을 열거나 파싱하지 않고 대상 경로에서 검사 대상 파일 목록과 파일 크기만 수집하여 출력합니다.

## 5. Nocturne 네이티브 디자인 (Phase 4 승인)

- 루트 `DESIGN.md`의 고정 팔레트를 WPF `Themes/Nocturne.xaml`의 의미 리소스와 OS 테마 적용 코드에 매핑한다.
- 기본은 Windows 앱 라이트/다크 설정이며 실행 중 변경을 반영한다. 고대비에서는 시스템 색상을 우선한다.
- 기존 폴더 트리, 파일 목록, 상세 패널, 분할 크기, 스캔 옵션·명령과 CLI 계약을 유지한다. 별도 UI 의존성이나 테마 선택 화면은 추가하지 않는다.
- 하드코딩한 배경·본문·경계·오류색을 DynamicResource로 교체한다. 행 상태는 기존 상태 문구와 함께 표시하고 선택된 행의 대비를 우선한다.
- Segoe UI/Windows 한글 폴백, 읽을 수 있는 보조 글자(최소 12 DIP), 네이티브 포커스 및 비활성 상태를 유지한다.
