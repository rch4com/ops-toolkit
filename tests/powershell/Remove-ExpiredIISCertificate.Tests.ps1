#Requires -Version 5.1

Describe 'Remove-ExpiredIISCertificate.ps1' {
    BeforeAll {
        $script:ScriptPath = Join-Path -Path $PSScriptRoot -ChildPath '..\..\scripts\iis\Remove-ExpiredIISCertificate\Remove-ExpiredIISCertificate.ps1'
        $script:ResolvedScriptPath = (Resolve-Path -LiteralPath $script:ScriptPath).Path
        $script:ToolDir = Split-Path -Parent $script:ResolvedScriptPath
    }

    Context '1. 필수 6대 문서 및 스크립트 파일 존재 검증' {
        It '도구 폴더 내에 필수 6대 문서(README, AGENTS, INTENT, SPEC, PLAN, REVIEW)가 모두 존재해야 한다' {
            $requiredDocs = @('README.md', 'AGENTS.md', 'INTENT.md', 'SPEC.md', 'PLAN.md', 'REVIEW.md')
            foreach ($doc in $requiredDocs) {
                $docPath = Join-Path -Path $script:ToolDir -ChildPath $doc
                Test-Path -LiteralPath $docPath | Should -BeTrue
            }
        }

        It 'PowerShell 문법 오류(ParseError)가 없어야 한다' {
            $tokens = $null
            $errors = $null
            $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                $script:ResolvedScriptPath,
                [ref]$tokens,
                [ref]$errors
            )
            $ast | Should -Not -BeNullOrEmpty
            $errors.Count | Should -Be 0
        }
    }

    Context '2. 파라미터 계약 및 안전장치(SupportsShouldProcess, LogDirectory) 검증' {
        BeforeAll {
            $script:CommandInfo = Get-Command -Name $script:ResolvedScriptPath
        }

        It 'SupportsShouldProcess(-WhatIf, -Confirm)가 활성화되어 있어야 한다' {
            $script:CommandInfo.Parameters.ContainsKey('WhatIf') | Should -BeTrue
            $script:CommandInfo.Parameters.ContainsKey('Confirm') | Should -BeTrue
        }

        It '필수 스위치(-List, -Execute, -Help, -InstallMissingIISComponents, -PassThru, -LogDirectory)를 지원해야 한다' {
            $expectedParams = @('List', 'Execute', 'Help', 'InstallMissingIISComponents', 'LogDirectory', 'PassThru')
            foreach ($p in $expectedParams) {
                $script:CommandInfo.Parameters.ContainsKey($p) | Should -BeTrue
            }
        }

        It '-LogDirectory 기본값이 현재 실행 폴더 하위(Logs)로 설정되어 있어야 한다' {
            $content = Get-Content -LiteralPath $script:ResolvedScriptPath -Raw
            $content | Should -Match "Join-Path -Path \(Get-Location\)\.Path -ChildPath 'Logs'"
        }
    }

    Context '3. 비관리자 안전 실행 모드(-Help 및 인자 검증) 동작 확인' {
        It '-Help 실행 시 오류 없이 종료되어야 한다' {
            { & $script:ResolvedScriptPath -Help *> $null } | Should -Not -Throw
        }

        It '파라미터 없이 실행 시 안내 후 오류 없이 종료되어야 한다' {
            { & $script:ResolvedScriptPath *> $null } | Should -Not -Throw
        }

        It '-List와 -Execute 동시 전달 시 오류 없이 안내 후 종료되어야 한다' {
            { & $script:ResolvedScriptPath -List -Execute *> $null } | Should -Not -Throw
        }
    }
}
