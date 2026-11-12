#Requires -Version 5.1

Describe 'PSJobScheduler' {
    BeforeAll {
        $script:ToolDir = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\scripts\scheduler\PSJobScheduler')).Path
        $script:ScheduleScript = Join-Path $script:ToolDir 'schedule.ps1'
        $script:ServiceHostScript = Join-Path $script:ToolDir 'schedule-service-host.ps1'
        $script:InstallScript = Join-Path $script:ToolDir 'Install-PSSchedule-NSSM.ps1'
        $script:UninstallScript = Join-Path $script:ToolDir 'Uninstall-PSSchedule-NSSM.ps1'
        $script:StatusScript = Join-Path $script:ToolDir 'Get-JobStatus.ps1'
        $script:TestCronScript = Join-Path $script:ToolDir 'Test-CronSchedule.ps1'
    }

    Context '1. 필수 6대 문서, nssm.exe 및 샘플 작업(jobs/) 존재 검증' {
        It '도구 폴더 내에 필수 6대 문서(README, AGENTS, INTENT, SPEC, PLAN, REVIEW)가 모두 존재해야 한다' {
            $requiredDocs = @('README.md', 'AGENTS.md', 'INTENT.md', 'SPEC.md', 'PLAN.md', 'REVIEW.md')
            foreach ($doc in $requiredDocs) {
                Test-Path -LiteralPath (Join-Path $script:ToolDir $doc) | Should -BeTrue
            }
        }

        It 'Windows 서비스 등록에 필요한 nssm.exe와 6개 샘플 작업 파일이 존재해야 한다' {
            Test-Path -LiteralPath (Join-Path $script:ToolDir 'nssm.exe') | Should -BeTrue
            $jobFiles = @(Get-ChildItem -Path (Join-Path $script:ToolDir 'jobs') -Filter '*.ps1' -File)
            $jobFiles.Count | Should -Be 6
        }

        It '모든 스크립트에 PowerShell 문법 오류(ParseError) 및 구 사내 명칭(Feelanet-)이 없어야 한다' {
            $scripts = @(
                $script:ScheduleScript,
                $script:ServiceHostScript,
                $script:InstallScript,
                $script:UninstallScript,
                $script:StatusScript,
                $script:TestCronScript
            )
            foreach ($s in $scripts) {
                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($s, [ref]$tokens, [ref]$errors)
                $ast | Should -Not -BeNullOrEmpty
                $errors.Count | Should -Be 0

                $raw = Get-Content -LiteralPath $s -Raw
                $raw | Should -Not -Match 'Feelanet-'
            }
        }
    }

    Context '2. Cron-Safe 파서 및 다음 실행 시각 검증 (Test-CronSchedule.ps1)' {
        It 'jobs 폴더 내 6개 샘플 작업의 Cron-Safe 문법이 모두 유효(Valid=True)하고 NextRun이 계산되어야 한다' {
            $results = @(& $script:TestCronScript -PassThru 6>$null)
            $results.Count | Should -Be 6
            foreach ($r in $results) {
                $r.Valid | Should -BeTrue
                $r.NextRun | Should -Not -BeNullOrEmpty
            }
        }

        It '부가 옵션(TIMEOUT, CB, NOW, LOG, CONCURRENT)이 정확히 파싱되어야 한다' {
            $sample = 'custom_job X@10 9-18 L X MON-FRI TIMEOUT=120 CB=4 NOW=1 LOG=1 CONCURRENT=1'
            $res = @(& $script:TestCronScript -FileNames $sample -PassThru 6>$null)
            $res.Count | Should -Be 1
            $res[0].Valid | Should -BeTrue
            $res[0].Prefix | Should -Be 'custom_job'
            $res[0].TimeoutSec | Should -Be 120
            $res[0].CircuitN | Should -Be 4
            $res[0].NowOpt | Should -BeTrue
            $res[0].LogOpt | Should -BeTrue
            $res[0].Concurrent | Should -BeTrue
        }
    }

    Context '3. 대시보드 및 Dry-Run(-WhatIf) 안전장치 검증' {
        It 'Get-JobStatus.ps1 -PassThru 실행 시 6개 샘플 작업의 상태 객체가 반환되어야 한다' {
            $statusList = @(& $script:StatusScript -PassThru 6>$null)
            $statusList.Count | Should -Be 6
            $statusList[0].Status | Should -Be 'IDLE'
        }

        It 'Install-PSSchedule-NSSM.ps1 및 Uninstall-PSSchedule-NSSM.ps1가 -WhatIf를 안전하게 지원해야 한다' {
            { & $script:InstallScript -WhatIf *> $null } | Should -Not -Throw
            { & $script:UninstallScript -WhatIf *> $null } | Should -Not -Throw
        }

        It 'schedule.ps1 -RunOnce -WhatIf 실행 시 오류 없이 1회 시뮬레이션 후 종료되어야 한다' {
            { & $script:ScheduleScript -RunOnce -WhatIf *> $null } | Should -Not -Throw
            # Clean up any log dir created during test
            $logDir = Join-Path $script:ToolDir 'logs'
            $stateDir = Join-Path $script:ToolDir 'state'
            if (Test-Path -LiteralPath $logDir) { Remove-Item -LiteralPath $logDir -Recurse -Force -ErrorAction SilentlyContinue }
            if (Test-Path -LiteralPath $stateDir) { Remove-Item -LiteralPath $stateDir -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
}
