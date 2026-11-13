# Publish-SingleFileTools

> 한 줄 요약: `ops-toolkit` 내 C# (.NET 10) 실행형 도구(`LdapAuthTester`, `MIPLabelCustomCheck.Explorer`)를 현장 서버/PC 배포용 단일 실행 파일(`.exe`)로 일괄 또는 개별 패키징하는 PowerShell 7 빌드 유틸리티입니다.

## 1. 제작 취지 (Why this tool?)
- 운영 서버나 폐쇄망 진단 PC에 도구를 배포할 때 여러 DLL과 설정 파일을 함께 복사하면 누락이나 버전 불일치가 발생하기 쉽습니다.
- `dotnet publish -p:PublishSingleFile=true` 옵션을 표준화하여 프레임워크 의존(Framework-Dependent) 또는 자체 포함(Self-Contained) 단일 실행 파일(`.exe`)을 일관된 경로(`artifacts/publish/`)에 생성합니다.
- `-WhatIf` 시뮬레이션을 통해 실제 빌드를 수행하기 전에 대상 프로젝트 경로, 출력 디렉터리, 런타임 식별자(RID) 구성을 안전하게 사전 점검할 수 있습니다.

## 2. 실행 환경 및 사전 요구사항
- **PowerShell 7+ (`pwsh`)**
- **.NET 10 SDK (`10.0.100` 이상)**
- 일반 사용자 권한으로 실행 가능

## 3. 사용법 및 옵션 (Usage & Options)

| 옵션 / 파라미터 | 필수 여부 | 기본값 | 설명 |
| :--- | :---: | :---: | :--- |
| `-ToolName` | 선택 | `'All'` | 배포할 도구 선택 (`All`, `LdapAuthTester`, `MIPLabelCustomCheck.Explorer`) |
| `-Runtime` | 선택 | `'win-x64'` | 대상 런타임 식별자(RID) (예: `win-x64`, `win-arm64`) |
| `-Configuration` | 선택 | `'Release'` | 빌드 구성 (`Release` 또는 `Debug`) |
| `-OutputDirectory` | 선택 | `<repoRoot>\artifacts\publish` | 단일 파일 결과물이 저장될 루트 출력 폴더 |
| `-SelfContained` | 선택 | `false` | `.NET 10` 런타임 미설치 환경을 위한 자체 포함(Self-Contained) 배포 여부 |
| `-WhatIf` | 선택 | `false` | 실제 `dotnet publish`를 실행하지 않고 배포 대상 및 결과 객체만 시뮬레이션 |

## 4. 실행 예제 (Examples)

### Dry-Run (안전 시뮬레이션)
```powershell
pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -WhatIf
```

### 모든 실행형 도구 단일 파일 배포 (Framework-Dependent)
```powershell
pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -ToolName All
```

### 특정 도구 자체 포함(Self-Contained) 단일 파일 배포
```powershell
pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -ToolName LdapAuthTester -SelfContained
```
