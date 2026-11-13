#Requires -Version 7.0
<#
.SYNOPSIS
    ops-toolkit 내 C# (.NET 10) 실행형 도구를 단일 실행 파일(.exe)로 게시(Publish)합니다.

.DESCRIPTION
    LdapAuthTester 및 MIPLabelCustomCheck.Explorer 프로젝트를 dotnet publish -p:PublishSingleFile=true 옵션으로 빌드하여
    지정된 출력 폴더(기본값: artifacts/publish)에 패키징합니다.
    SupportsShouldProcess(-WhatIf, -Confirm)를 지원하므로 실제 빌드를 수행하기 전에 배포 대상을 안전하게 시뮬레이션할 수 있습니다.

.PARAMETER ToolName
    게시할 도구 이름입니다. 'All', 'LdapAuthTester', 'MIPLabelCustomCheck.Explorer' 중 하나를 지정합니다. (기본값: 'All')

.PARAMETER Runtime
    대상 런타임 식별자(RID)입니다. (기본값: 'win-x64')

.PARAMETER Configuration
    빌드 구성입니다. (기본값: 'Release')

.PARAMETER OutputDirectory
    게시된 단일 실행 파일이 저장될 루트 출력 디렉터리입니다. 지정하지 않으면 저장소 루트 하위 'artifacts\publish'를 사용합니다.

.PARAMETER SelfContained
    .NET 10 런타임이 설치되지 않은 환경을 위해 자체 포함(Self-Contained) 단일 실행 파일로 게시할지 여부입니다.

.EXAMPLE
    pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -WhatIf
    실제 빌드를 수행하지 않고 모든 대상 도구의 게시 경로 및 옵션을 시뮬레이션하여 출력합니다.

.EXAMPLE
    pwsh -NoProfile -File .\scripts\build\Publish-SingleFileTools\Publish-SingleFileTools.ps1 -ToolName LdapAuthTester -SelfContained
    LdapAuthTester 프로젝트를 win-x64 자체 포함(Self-Contained) 단일 실행 파일로 게시합니다.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
[OutputType([PSCustomObject])]
param(
    [Parameter()]
    [ValidateSet('All', 'LdapAuthTester', 'MIPLabelCustomCheck.Explorer')]
    [string]$ToolName = 'All',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Runtime = 'win-x64',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Configuration = 'Release',

    [Parameter()]
    [string]$OutputDirectory,

    [Parameter()]
    [switch]$SelfContained
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path -Path $PSScriptRoot -ChildPath '..\..\..')).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path -Path $repoRoot -ChildPath 'artifacts\publish'
}

$toolDefinitions = @(
    [PSCustomObject]@{
        Name         = 'LdapAuthTester'
        RelativePath = 'src\LdapAuthTester\LdapAuthTester.csproj'
    },
    [PSCustomObject]@{
        Name         = 'MIPLabelCustomCheck.Explorer'
        RelativePath = 'src\MIPLabelCustomCheck\MIPLabelCustomCheck.Explorer\MIPLabelCustomCheck.Explorer.csproj'
    }
)

$selectedTools = if ($ToolName -eq 'All') {
    $toolDefinitions
}
else {
    $toolDefinitions | Where-Object { $_.Name -eq $ToolName }
}

$isSelfContained = $SelfContained.IsPresent
$selfContainedArg = if ($isSelfContained) { 'true' } else { 'false' }

foreach ($tool in $selectedTools) {
    $targetProject = Join-Path -Path $repoRoot -ChildPath $tool.RelativePath
    if (-not (Test-Path -LiteralPath $targetProject)) {
        throw "대상 프로젝트 파일을 찾을 수 없습니다: $targetProject"
    }

    $toolOutDir = Join-Path -Path $OutputDirectory -ChildPath (Join-Path -Path $tool.Name -ChildPath $Runtime)
    $actionDescription = "Publish single-file executable ($Runtime, SelfContained=$isSelfContained)"

    if ($PSCmdlet.ShouldProcess($targetProject, $actionDescription)) {
        Write-Verbose -Message "Publishing $($tool.Name) to $toolOutDir ..."
        $publishArgs = @(
            'publish',
            $targetProject,
            '-c', $Configuration,
            '-r', $Runtime,
            '--self-contained', $selfContainedArg,
            '-p:PublishSingleFile=true',
            '-o', $toolOutDir,
            '--nologo'
        )

        & dotnet @publishArgs | Write-Verbose
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish 실패 ($($tool.Name), 종료 코드: $LASTEXITCODE)"
        }

        [PSCustomObject]@{
            Timestamp       = (Get-Date).ToString('o')
            ToolName        = $tool.Name
            ProjectPath     = $targetProject
            OutputDirectory = $toolOutDir
            Runtime         = $Runtime
            SelfContained   = $isSelfContained
            Status          = 'Published'
        }
    }
    else {
        [PSCustomObject]@{
            Timestamp       = (Get-Date).ToString('o')
            ToolName        = $tool.Name
            ProjectPath     = $targetProject
            OutputDirectory = $toolOutDir
            Runtime         = $Runtime
            SelfContained   = $isSelfContained
            Status          = 'WhatIf'
        }
    }
}
