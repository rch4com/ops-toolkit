# SPEC.md — Publish-SingleFileTools

## 1. 입출력 계약 (Input / Output Contract)
- **입력 파라미터**:
  - `-ToolName` (`[string]`, 기본값 `'All'`, 허용값: `'All'`, `'LdapAuthTester'`, `'MIPLabelCustomCheck.Explorer'`)
  - `-Runtime` (`[string]`, 기본값 `'win-x64'`)
  - `-Configuration` (`[string]`, 기본값 `'Release'`)
  - `-OutputDirectory` (`[string]`, 기본값 `<repoRoot>\artifacts\publish`)
  - `-SelfContained` (`[switch]`, 기본값 `$false`)
  - `-WhatIf` / `-Confirm` (`SupportsShouldProcess = $true`)
- **출력 포맷**: 각 대상 도구별 `[PSCustomObject]` 반환
  - `Timestamp`: ISO 8601 타임스탬프 (`[string]`)
  - `ToolName`: 도구 이름 (`[string]`)
  - `ProjectPath`: `.csproj` 전체 경로 (`[string]`)
  - `OutputDirectory`: 도구별 게시 출력 폴더 경로 (`[string]`)
  - `Runtime`: 대상 RID (`[string]`)
  - `SelfContained`: 자체 포함 여부 (`[bool]`)
  - `Status`: `'Published'` 또는 `'WhatIf'` (`[string]`)
- **종료 코드(Exit Code)**: `0`(성공), `1`(`dotnet publish` 실패 또는 프로젝트 경로 누락)

## 2. 핵심 동작 로직 및 Dry-Run (`-WhatIf`) 처리
- 스크립트 위치를 기준으로 저장소 루트(`repoRoot`)를 계산하고 대상 도구별 `.csproj` 경로를 매핑합니다.
- `$PSCmdlet.ShouldProcess($projectPath, ...)`가 `$false`인 경우(`-WhatIf`), `dotnet publish` 및 폴더 생성을 생략하고 `Status = 'WhatIf'`인 결과 객체를 즉시 반환합니다.
- `$PSCmdlet.ShouldProcess`가 `$true`인 경우, `dotnet publish`에 `-c $Configuration -r $Runtime --self-contained <true|false> -p:PublishSingleFile=true -o $toolOutDir` 인자를 전달하여 실행합니다.

## 3. 예외 처리 (Error Handling)
- 대상 `.csproj` 파일이 존재하지 않거나 `dotnet publish` 종료 코드(`$LASTEXITCODE`)가 `0`이 아니면 명시적인 예외(`throw`)를 발생시켜 즉시 중단합니다.
