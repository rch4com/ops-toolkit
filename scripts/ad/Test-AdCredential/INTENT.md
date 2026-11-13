# Intent

운영자가 AD 인증 실패와 TCP 연결 문제를 PowerShell에서 독립적으로 구분하도록 Utilities 도구를 이관한다. 사용자는 Phase 1 권장안(PS7, 객체 출력, 표준 종료 코드, 실행별 LDAPS 검사 생략)을 승인했다.

비밀번호는 SecureString만 받으며 파일에 저장하지 않는다. AD 객체 변경, 배치 인증, 자동 재시도, 스캐폴더, 기존 데이터 이관은 범위 밖이다.
