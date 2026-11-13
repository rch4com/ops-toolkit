# Specification

PS7 advanced script. `Server`, `Username`, `SecureString Password`, `UseLdaps`, `Port=0`(자동), `TimeoutSeconds=15`, `TestConnectionOnly`, `SkipCertificateCheck`, ShouldProcess(ConfirmImpact Medium).

검증 오류는 종료 2/FailureKind Usage. 실행 실패는 종료 1과 Credential/Connection/Tls/Runtime 분류. 성공과 WhatIf는 종료 0. 결과 객체 스키마는 README 참조. 사용자 입력이나 서버 오류 원문에 포함된 비밀번호를 출력하지 않는다.

WhatIf는 계획만 출력하고 서버 자동 감지·네트워크·암호 입력·bind를 하지 않는다. TCP-only는 비밀번호/계정을 요구하지 않고 연결만 검사한다. 실제 인증은 ShouldProcess 승인 후 1회 Negotiate bind. LDAPS는 기본 인증서 검증, 명시적인 SkipCertificateCheck에만 경고 후 생략. LDAP와 생략 조합은 오류.

스크립트를 dot-source하면 함수만 정의하고 실행하지 않는다. 직접 실행은 결과와 종료 코드를 제공한다. 네트워크 객체/타이머는 finally에서 해제한다.

호스트 경계: PowerShell 바인더가 본문 전에 거부하는 형 변환/알 수 없는 파라미터 오류는 호스트 기본 오류와 종료 1이다. 구조화된 Usage/종료 2 계약은 바인딩이 성공한 뒤 본문에서 검증하는 입력에 적용한다. 별도 인자 파서를 추가하지 않는다.
