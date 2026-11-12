<#
    .SYNOPSIS
    1분 하트비트 작업

    .DESCRIPTION
    1분마다 실행됩니다.
    파일명: job_1min X@1 X X X X.ps1
    분: X@1 (1분 간격), 시: X, 일: X, 월: X, 요일: X

    시뮬레이션: 시스템 상태 모니터링 또는 하트비트 체크에 필수적입니다.
#>

$Name = "Heartbeat"
$Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

Write-Host "[$Name] 동작 중 (시간: $Timestamp)"

# Simulate a quick check
if (Test-Connection -ComputerName "localhost" -Count 1 -Quiet) {
    Write-Host "[$Name] 시스템 응답 정상."
}
else {
    Write-Warning "[$Name] 로컬호스트 Ping 실패!"
}