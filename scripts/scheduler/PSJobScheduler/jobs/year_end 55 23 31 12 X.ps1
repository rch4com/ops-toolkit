<#
    .SYNOPSIS
    연말 데이터 보관

    .DESCRIPTION
    12월 31일 23:55 (새해 5분 전)에 실행됩니다.
    파일명: year_end 55 23 31 12 X.ps1
    분: 55, 시: 23, 일: 31, 월: 12 (12월), 요일: X

    시뮬레이션: 연간 데이터 아카이빙 작업을 수행합니다.
#>

$Name = "YearEndArchive"
$Year = (Get-Date).Year
Write-Host "[$Name] $Year 년도 연말 데이터 보관 작업을 시작합니다."

Write-Host "[$Name] 데이터 보관 중... (1년에 한 번 실행됩니다!)"
Start-Sleep -Seconds 3

Write-Host "[$Name] 새해 복 많이 받으세요!"