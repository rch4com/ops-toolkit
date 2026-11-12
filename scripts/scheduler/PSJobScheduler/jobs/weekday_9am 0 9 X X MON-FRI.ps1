<#
    .SYNOPSIS
    주중 아침 업무 리포트

    .DESCRIPTION
    월-금 오전 09:00에 실행됩니다.
    파일명: weekday_9am 0 9 X X MON-FRI.ps1
    분: 0, 시: 9, 일: X, 월: X, 요일: MON-FRI(월~금)

    시뮬레이션: 팀을 위한 일일 상태 리포트를 생성합니다.
#>

$Name = "MorningReport"
Write-Host "[$Name] 아침 업무 리포트 생성 중..."

# Simulate data gathering
$users = Get-ChildItem "C:\Users" -Directory | Select-Object -ExpandProperty Name
$Disk = Get-PSDrive C | Select-Object Used, Free

Write-Host "[$Name] 사용자 목록: $($users -join ', ')"
Write-Host "[$Name] C드라이브: 사용=$($Disk.Used), 여유=$($Disk.Free)"

# Simulate sending email
Write-Host "[$Name] team@example.com로 이메일 발송 완료."