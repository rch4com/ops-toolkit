# Test-AdCredential agent instructions

루트 AGENTS.md와 이 폴더의 INTENT/SPEC/PLAN/REVIEW 순서로 읽는다.

- 테스트: `Invoke-Pester ./tests/powershell/Test-AdCredential.Tests.ps1 -Output Detailed`
- 분석: `Invoke-ScriptAnalyzer ./scripts/ad/Test-AdCredential/Test-AdCredential.ps1`
- 안전 검사: `./scripts/ad/Test-AdCredential/Test-AdCredential.ps1 -Server dc.example.test -Username example -WhatIf`
- 테스트에서는 실제 AD 바인딩을 하지 않는다. dot-source는 함수만 로드한다.
- ShouldProcess 이전에 네트워크·비밀번호 입력 금지. 자동 재시도와 평문 비밀번호 파라미터 금지.
