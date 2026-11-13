# INTENT.md — Publish-SingleFileTools

## 1. 해결하려는 문제 (Problem)
- `ops-toolkit` 내 C# 도구(`LdapAuthTester`, `MIPLabelCustomCheck.Explorer`)를 운영 환경에 전달할 때 각 프로젝트별 `dotnet publish` 명령어와 단일 파일 옵션(`-p:PublishSingleFile=true`)을 매번 수동 입력해야 하는 번거로움이 있습니다.
- 표준화된 게시 스크립트가 없으면 출력 경로가 파편화되거나 잘못된 런타임 옵션으로 배포될 위험이 있습니다.

## 2. 기대 결과 및 성공 기준 (Expected Outcome)
- 단일 스크립트 호출로 전체(`All`) 또는 개별 C# 도구를 `artifacts/publish/<ToolName>/<Runtime>` 경로에 단일 실행 파일로 패키징합니다.
- `-WhatIf` 옵션을 지원하여 실제 빌드를 수행하지 않고도 배포 대상과 설정을 구조화된 객체(`PSCustomObject`)로 확인할 수 있습니다.

## 3. Non-Goals (하지 않을 것 / 제약 사항)
- 코드 서명(Authenticode) 인증서 주입이나 외부 아티팩트 저장소(NuGet/GitHub Releases) 자동 업로드는 수행하지 않습니다.
- 라이브러리 전용 프로젝트(`MIPLabelCustomCheck`)나 벤치마크/테스트 프로젝트는 단일 실행 파일 배포 대상에 포함하지 않습니다.
