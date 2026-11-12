<#
    .SYNOPSIS
    5분 서비스 점검

    .DESCRIPTION
    5분마다 실행됩니다.
    파일명: job_5min X@5 X X X X.ps1
    분: X@5 (5분 간격), 시: X, 일: X, 월: X, 요일: X

    시뮬레이션: 주요 서비스의 상태를 확인합니다.
#>

$Name = "ServiceMonitor"
Write-Host "[$Name] 서비스 상태 확인 중..."

# Example: Check if specific service is running (e.g., Spooler)
$serviceName = "Spooler"
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if ($service) {
    Write-Host "[$Name] 서비스 '$serviceName' 상태: $($service.Status)"
    if ($service.Status -ne 'Running') {
        Write-Warning "[$Name] 서비스 '$serviceName'가 실행 중이 아닙니다."
        # Optional: Restart-Service -Name $serviceName
    }
}
else {
    Write-Host "[$Name] 서비스 '$serviceName'를 찾을 수 없습니다."
}