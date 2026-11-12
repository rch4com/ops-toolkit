# MIPLabelCustomCheck

> **한 줄 요약**: Microsoft Purview(MIP) SDK 없이 Office, PDF, 이미지(PNG/JPEG/TIFF), 이메일(MSG/EML) 파일의 민감도 레이블(Sensitivity Label) GUID와 암호화 보호 상태를 직접 고속으로 판별하는 무의존성 .NET 10 라이브러리 및 하이브리드(WPF Explorer GUI + CLI) 도구입니다.

---

## 1. 제작 취지 (Why this tool?)
사내 문서 보안(DRM/DLP/AIP/Purview) 운영 중 특정 문서나 폴더 내 대량 파일에 **Microsoft Purview 민감도 레이블이 정상 적용되어 있는지, 어떤 레이블 GUID/테넌트(`SiteId`)/방식(`Standard`/`Privileged`)으로 태깅되었는지, 혹은 암호화(`Native` / `.pfile`)로 보호되어 있는지** 즉시 확인해야 하는 경우가 많습니다.

공식 MIP SDK는 네이티브 C++ 런타임과 인증 연동이 필요해 현장 서버나 폐쇄망 PC에서 단독 진단 도구로 쓰기 무겁습니다. `MIPLabelCustomCheck`는 **외부 NuGet 의존성 0개(순수 .NET 10 BCL)**로 파일 컨테이너의 메타데이터 영역만 직접 파싱하여 파일당 sub-millisecond(0.01~0.2ms) 속도로 레이블과 보호 상태를 진단합니다.

### 주요 구성
1. **`MIPLabelCustomCheck` (Core Library, `net10.0`)**: 외부 의존성 0개의 초경량 MIP 레이블/보호 상태 검출 라이브러리
2. **`MIPLabelCustomCheck.Explorer` (WPF GUI + CLI Hybrid, `net10.0-windows`)**:
   - **인자 없이 실행 시**: 드라이브/폴더 트리 탐색, 병렬 고속 스캔, 레이블 상세 정보 및 파일별 검사 소요 시간을 보여주는 **WPF 데스크톱 탐색기 GUI** 실행
   - **CLI 인자 전달 시**: 지정한 파일/폴더의 레이블 상태를 콘솔 표 또는 `--json`으로 출력하고 종료 코드를 반환하는 **CLI 도구**로 동작
3. **`MIPLabelCustomCheck.Benchmarks` (`net10.0`)**: 포맷별 단건/병렬 처리 및 512 MiB 대용량 스파스 파일 성능을 검증하는 `BenchmarkDotNet` 스위트

---

## 2. 실행 환경 및 사전 요구사항
- **OS**: Windows 10 / 11, Windows Server 2019 / 2022 / 2025 (코어 라이브러리는 Cross-platform `net10.0` 호환)
- **Runtime**: .NET 10 Desktop Runtime (`net10.0-windows`)
- **권한**: 일반 사용자 권한 (대상 파일 읽기 권한만 필요)

---

## 3. 사용법 및 옵션 (Usage & Options)

### 3.1 GUI 모드 실행 (Visual Studio 또는 명령줄)
- **Visual Studio 단독 실행 (`F5`)**: `src/MIPLabelCustomCheck/MIPLabelCustomCheck.sln` 파일을 열면 `MIPLabelCustomCheck.Explorer`가 기본 시작 프로젝트로 설정되어 있어 즉시 `F5`로 실행/디버깅할 수 있습니다.
- **명령줄 실행**:
```powershell
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj
```

### 3.2 CLI 모드 옵션

| 옵션 | 단축 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :---: | :--- |
| `--path <path>` | `-p` | 조건부 필수 | - | 검사할 파일 또는 폴더 경로 |
| `--recursive` | `-r` | 선택 | `false` | 폴더 지정 시 하위 디렉터리까지 재귀 스캔 |
| `--all-files` | `-a` | 선택 | `false` | 알려진 레이블 지원 확장자 외의 모든 파일도 검사 |
| `--details` | `-d` | 선택 | `false` | 레이블 상세 메타데이터(`SiteId`, `Name`, `Method`, `ContentBits`, `SetDate`) 포함 |
| `--dry-run` | - | 선택 | `false` | 실제 파일 파싱 없이 스캔 대상 파일 목록과 크기만 사전 점검 |
| `--json` | - | 선택 | `false` | 결과를 구조화된 JSON 포맷으로 출력 |
| `--verbose` | `-v` | 선택 | `false` | 파일별 소요 시간(ms) 및 보호 상세(`ProtectionDetail`) 출력 |
| `--gui` | - | 선택 | `false` | WPF Explorer GUI 창 실행 |
| `--help` | `-h` | 선택 | - | 도움말 출력 |
| `--version` | - | 선택 | - | 버전 정보 출력 |

---

## 4. 지원 포맷 및 판별 상태

| 포맷 | 확장자 | 메타데이터 탐색 위치 |
| :--- | :--- | :--- |
| **OOXML Office** | `.docx`, `.xlsx`, `.pptx` (및 매크로/템플릿 변형) | `docMetadata/LabelInfo.xml` 및 `docProps/custom.xml` (`MSIP_Label_{guid}_*`), 테넌트(`SiteId`) 우선순위 적용 |
| **Legacy Office (CFB)** | `.doc`, `.xls`, `.ppt` (및 템플릿 변형) | `DocumentSummaryInformation` 사용자 정의 속성 및 `DataSpaces/TransformInfo/LabelInfo` 스트림 |
| **PDF** | `.pdf` | `Info` 사전 및 문서 카탈로그 `/Metadata` XMP 스트림 (`/Encrypt` 감지 포함) |
| **PNG / JPEG / TIFF** | `.png`, `.jpg`, `.jpeg`, `.tif`, `.tiff` | PNG `iTXt` (`XML:com.adobe.xmp`), JPEG `APP1` XMP, TIFF Tag 700 XMP 패킷 |
| **Outlook MSG / EML** | `.msg`, `.eml` | MSG `__nameid_version1.0` (`PS_PUBLIC_STRINGS` `MSIP_Label_*`), EML RFC 5322 `MSIP_Labels` 헤더 |
| **Generic PFile** | `.pfile` | `.pfile` 시그니처 감지 (`ProtectedLabelUnknown` / `GenericPFile` 반환) |

---

## 5. 실행 예제 (Examples)

### 1) 샘플 픽스처 폴더 전체 레이블 및 상세 메타데이터 검사
```powershell
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj -- --path .\src\MIPLabelCustomCheck\Fixtures --details --verbose
```

### 2) Dry-Run: 실제 파일 파싱 없이 폴더 내 검사 대상 파일 목록만 사전 확인 (`--dry-run`)
```powershell
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj -- --path .\src\MIPLabelCustomCheck\Fixtures --recursive --dry-run
```

### 3) 단일 파일 검사 결과를 JSON으로 출력 (`--json`)
```powershell
dotnet run --project .\src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj -- --path ".\src\MIPLabelCustomCheck\Fixtures\MIP Label.docx" --details --json
```

### 4) C# 코드에서 코어 라이브러리(`MipLabelChecker`) 직접 사용
```csharp
using MIPLabelCustomCheck;

var checker = new MipLabelChecker();
LabelCheckResult result = checker.Check(@"C:\Docs\Sample.docx", LabelCheckOptions.WithDetails);

if (result.IsLabeled)
{
    foreach (LabelDetail detail in result.Details)
    {
        Console.WriteLine($"LabelId: {detail.Id}, Name: {detail.Name}, SiteId: {detail.SiteId}, Method: {detail.Method}");
    }
}
else if (result.Status == LabelStatus.ProtectedLabelUnknown)
{
    Console.WriteLine($"암호화 보호된 파일 (레이블 메타데이터 비공개): {result.Protection} ({result.ProtectionDetail})");
}
```
