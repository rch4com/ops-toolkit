# GitHub Copilot Instructions for `ops-toolkit`

이 저장소는 사내 인프라 및 시스템 운영 관리를 위한 **PowerShell 7 (`pwsh`)** 및 **C# (.NET 10)** 유틸리티 모음입니다.
코드를 제안하거나 생성할 때 루트 디렉터리의 다음 문서들을 기준으로 삼으세요:

1. **[AGENTS.md](../AGENTS.md)**: 전체 코딩 컨벤션, 디렉터리 구조, 빌드/테스트 명령어
2. **[INTENT.md](../INTENT.md)**: 프로젝트 목적 및 운영 안전 수칙
3. **[SPEC.md](../SPEC.md)**: PowerShell 스크립트 및 .NET 10 CLI 기술 명세
4. **[PLAN.md](../PLAN.md)**: 구현 계획 및 작업 단계
5. **[REVIEW.md](../REVIEW.md)**: 코드 리뷰 및 보안/품질 체크리스트

## 핵심 준수 사항
- **신규 도구 추가 시 인터뷰 및 6대 문서 필수화**: 새로운 유틸리티나 PowerShell 프로그램을 추가할 때는 먼저 사용자와 인터뷰를 진행하여 해당 도구 폴더 내에 `README.md`, `AGENTS.md`, `INTENT.md`, `SPEC.md`, `PLAN.md`, `REVIEW.md` 6개 문서를 생성해야 합니다.
- **PowerShell 7 (`scripts/`)**: `#Requires -Version 7.0`, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = 'Stop'`, 상태 변경 시 `[CmdletBinding(SupportsShouldProcess = $true)]` 필수 적용, 축약 별칭(Alias) 사용 금지, `[PSCustomObject]` 반환 지향.
- **C# .NET 10 (`src/`)**: `net10.0`, Nullable Reference Types 활성화, 모든 비동기 작업에 `CancellationToken` 전달, 상태 변경 CLI 도구에 `--dry-run` 옵션 지원.
- **보안**: 자격 증명, API 키, 내부 IP 주소의 하드코딩을 절대 금지합니다.

<!-- mermaid-ai-skills:start -->
## Mermaid Diagrams

When the user asks to create, edit, or visualize a diagram, follow the
instructions in `.github/instructions/mermaid.instructions.md`.
<!-- mermaid-ai-skills:end -->
