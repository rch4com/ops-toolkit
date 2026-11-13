# Test-AdCredential

AD 연결 장애와 자격 증명 실패를 분리하는 PowerShell 7 단일 LDAP/LDAPS Negotiate bind 진단 도구.

```powershell
./Test-AdCredential.ps1 -Server dc.example.test -Username operator -WhatIf
./Test-AdCredential.ps1 -Server localhost -Port 389 -TestConnectionOnly
$credential = Get-Credential
./Test-AdCredential.ps1 -Server dc.example.test -UseLdaps -Username $credential.UserName -Password $credential.Password
```

| 옵션 | 동작 |
|---|---|
| Server | 서버. 생략 시 USERDNSDOMAIN/현재 도메인 감지 |
| Username / Password | 실제 인증 시 계정과 SecureString. 비밀번호 생략 시 보안 프롬프트 |
| UseLdaps / Port | LDAPS 사용, 기본 포트 636/389. 명시 포트 1–65535 |
| TimeoutSeconds | TCP 및 bind 제한 시간, 1–300초, 기본 15 |
| TestConnectionOnly | TCP 연결만 검사. 인증·비밀번호 프롬프트 없음 |
| SkipCertificateCheck | LDAPS에만 허용. 실행별 테스트용이며 설정 저장 없음 |
| WhatIf / Confirm | 인증 전 ShouldProcess. WhatIf는 DNS/네트워크/암호 프롬프트 없이 계획만 반환 |

결과는 `PSCustomObject`이며 `Success, Status, FailureKind, ExitCode, Server, Port, Protocol, Message`를 포함한다. `ConvertTo-Json`/`Export-Csv`로 연결할 수 있다. 종료 코드는 0=성공/WhatIf/사용자 취소, 1=실행 실패, 2=잘못된 인자. `FailureKind`가 Credential/Connection/Tls/Runtime/Usage를 구분한다. 계획·취소 결과의 `Success`는 false이며 Status는 WhatIf/Skipped다.

인증은 1회만 시도하며 자동 재시도하지 않는다. 잘못된 자격 증명은 AD 계정 실패 횟수에 반영될 수 있다. 실제 AD 검증은 허가된 환경에서 실행한다. 파일·AD 객체를 변경하거나 사용자 정보/비밀번호를 저장하지 않는다.

PowerShell 자체 매개변수 바인딩 오류(예: `-Port not-a-number`, 알 수 없는 옵션)는 본문 실행 전에 호스트가 처리하므로 구조화된 결과 없이 종료 1이 된다. 스크립트 본문의 사용법 검증(포트 범위, 누락된 계정, 잘못된 옵션 조합)은 위의 종료 2 계약을 따른다.
