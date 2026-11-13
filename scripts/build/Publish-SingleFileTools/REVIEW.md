# REVIEW.md — Publish-SingleFileTools

- [x] **보안**: 코드 및 예제에 비밀번호, API 키, 사내 민감 IP가 하드코딩되지 않았는가?
- [x] **안전성**: `-WhatIf` 실행 시 디스크에 파일을 생성하거나 `dotnet publish`를 실행하지 않고 `Status = 'WhatIf'` 객체를 안전하게 반환하는가?
- [x] **사용성**: 다른 사용자가 이 폴더의 `README.md`만 읽고도 도구의 취지와 사용법을 즉시 이해할 수 있는가?
- [x] **품질**: `Invoke-ScriptAnalyzer` 경고/오류 0건 및 `Invoke-Pester` 단위 테스트를 모두 통과했는가?
