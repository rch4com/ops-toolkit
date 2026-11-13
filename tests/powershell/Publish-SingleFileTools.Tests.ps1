#Requires -Version 7.0

Describe 'Publish-SingleFileTools.ps1' {
    BeforeAll {
        $script:ScriptPath = Join-Path -Path $PSScriptRoot -ChildPath '..\..\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1'
        $script:ResolvedScriptPath = (Resolve-Path -LiteralPath $script:ScriptPath).Path
        $script:ToolDir = Split-Path -Parent $script:ResolvedScriptPath
    }

    Context '1. 필수 6대 문서 및 스크립트 문법 검증' {
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

    Context '2. 파라미터 계약 및 SupportsShouldProcess 검증' {
        BeforeAll {
            $script:CommandInfo = Get-Command -Name $script:ResolvedScriptPath
        }

        It 'SupportsShouldProcess(-WhatIf, -Confirm)가 활성화되어 있어야 한다' {
            $script:CommandInfo.Parameters.ContainsKey('WhatIf') | Should -BeTrue
            $script:CommandInfo.Parameters.ContainsKey('Confirm') | Should -BeTrue
        }

        It '필수 파라미터(ToolName, Runtime, Configuration, OutputDirectory, SelfContained)를 지원해야 한다' {
            $expectedParams = @('ToolName', 'Runtime', 'Configuration', 'OutputDirectory', 'SelfContained')
            foreach ($paramName in $expectedParams) {
                $script:CommandInfo.Parameters.ContainsKey($paramName) | Should -BeTrue
            }
        }
    }

    Context '3. -WhatIf Dry-Run 실행 검증' {
        It '-ToolName All -WhatIf 실행 시 디스크에 폴더/파일을 생성하지 않고 2개의 WhatIf 결과 객체를 반환해야 한다' {
            $tempOutDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("ops-toolkit-whatif-" + [guid]::NewGuid().ToString('N'))

            $results = & $script:ResolvedScriptPath -ToolName All -OutputDirectory $tempOutDir -WhatIf

            Test-Path -LiteralPath $tempOutDir | Should -BeFalse
            $results | Should -Not -BeNullOrEmpty
            @($results).Count | Should -Be 2
            $results[0].ToolName | Should -Be 'LdapAuthTester'
            $results[0].Status | Should -Be 'WhatIf'
            $results[1].ToolName | Should -Be 'MIPLabelCustomCheck.Explorer'
            $results[1].Status | Should -Be 'WhatIf'
        }

        It '-ToolName LdapAuthTester -SelfContained -WhatIf 실행 시 단일 WhatIf 결과 객체를 정확히 반환해야 한다' {
            $tempOutDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("ops-toolkit-whatif-single-" + [guid]::NewGuid().ToString('N'))

            $result = & $script:ResolvedScriptPath -ToolName LdapAuthTester -Runtime 'win-x64' -OutputDirectory $tempOutDir -SelfContained -WhatIf

            Test-Path -LiteralPath $tempOutDir | Should -BeFalse
            @($result).Count | Should -Be 1
            $result.ToolName | Should -Be 'LdapAuthTester'
            $result.Runtime | Should -Be 'win-x64'
            $result.SelfContained | Should -BeTrue
            $result.Status | Should -Be 'WhatIf'
        }
    }
}
