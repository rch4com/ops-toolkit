<#
    .SYNOPSIS
    월 마감 처리

    .DESCRIPTION
    매월 말일 18:30에 실행됩니다.
    파일명: month_end 30 18 L X X TIMEOUT=600 CB=3.ps1
    분: 30, 시: 18, 일: L(말일), 월: X, 요일: X
    옵션:
      - TIMEOUT=600: 작업이 10분(600초)을 초과하면 강제 종료합니다.
      - CB=3: 3회 연속 실패 시 서킷 브레이커가 작동합니다.

    시뮬레이션: 월말 대규모 데이터 처리 작업을 수행합니다.
#>

$Name = "MonthEndClose"
Write-Host "[$Name] 월 마감 처리를 시작합니다..."

# Simulate a long running task
for ($i = 1; $i -le 5; $i++) {
    Write-Host "[$Name] 배치 작업 처리 중... $i/5"
    Start-Sleep -Seconds 1
}

# Simulate random failure to test Circuit Breaker (CB=3)
$random = Get-Random -Minimum 0 -Maximum 10
if ($random -gt 8) {
    throw "서킷 브레이커 테스트를 위한 무작위 실패 발생!"
}

Write-Host "[$Name] 월 마감 처리가 완료되었습니다."