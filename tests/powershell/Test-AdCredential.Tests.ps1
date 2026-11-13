#Requires -Version 7.0

Describe 'Test-AdCredential safety contracts' {
    BeforeAll {
        $script:ToolPath = Join-Path $PSScriptRoot '../../scripts/ad/Test-AdCredential/Test-AdCredential.ps1'
        . $script:ToolPath
    }

    It 'WhatIf does not resolve servers, connect, bind or prompt for secrets' {
        Mock Resolve-AdEndpoint { throw 'Unexpected discovery' }
        Mock Test-AdTcpConnection { throw 'Unexpected TCP' }
        Mock Invoke-AdCredentialBind { throw 'Unexpected bind' }
        Mock Read-Host { throw 'Unexpected password prompt' }
        $result = Invoke-AdCredentialCheck -Server 'dc.example.test' -Username 'example' -WhatIf
        $result.Status | Should -Be 'WhatIf'
        $result.ExitCode | Should -Be 0
        Should -Invoke Resolve-AdEndpoint -Times 0
        Should -Invoke Test-AdTcpConnection -Times 0
        Should -Invoke Invoke-AdCredentialBind -Times 0
        Should -Invoke Read-Host -Times 0
    }

    It 'TCP-only succeeds without a username or password and never binds' {
        Mock Test-AdTcpConnection { $true }
        Mock Invoke-AdCredentialBind { throw 'Unexpected bind' }
        Mock Read-Host { throw 'Unexpected prompt' }
        $result = Invoke-AdCredentialCheck -Server localhost -TestConnectionOnly
        $result.Success | Should -BeTrue
        $result.Port | Should -Be 389
        Should -Invoke Invoke-AdCredentialBind -Times 0
        Should -Invoke Read-Host -Times 0
    }

    It 'maps connection failure to runtime code 1 with structured Connection kind' {
        Mock Test-AdTcpConnection { $false }
        $result = Invoke-AdCredentialCheck -Server localhost -TestConnectionOnly
        $result.ExitCode | Should -Be 1
        $result.FailureKind | Should -Be 'Connection'
    }

    It 'rejects certificate bypass over LDAP before a connection' {
        Mock Resolve-AdEndpoint { throw 'Unexpected discovery' }
        $result = Invoke-AdCredentialCheck -Server localhost -SkipCertificateCheck
        $result.ExitCode | Should -Be 2
        $result.FailureKind | Should -Be 'Usage'
        Should -Invoke Resolve-AdEndpoint -Times 0
    }

    It 'rejects an out-of-range port without network access' {
        $result = Invoke-AdCredentialCheck -Server localhost -Port 65536 -TestConnectionOnly
        $result.ExitCode | Should -Be 2
    }

    It 'does not classify invalid credentials as usage errors' {
        $exception = [System.DirectoryServices.Protocols.LdapException]::new(49)
        Get-AdFailureKind -Exception $exception | Should -Be 'Credential'
    }

    It 'returns process exit code 2 for invalid usage' {
        $output = & pwsh -NoProfile -File $script:ToolPath -Server localhost -Port 65536 -TestConnectionOnly
        $LASTEXITCODE | Should -Be 2
        ($output -join "`n") | Should -Match 'Usage'
    }

    It 'documents PowerShell host binding failures separately from script usage validation' {
        $output = & pwsh -NoProfile -File $script:ToolPath -Port not-a-number 2>&1
        $LASTEXITCODE | Should -Be 1
        ($output -join "`n") | Should -Match 'not-a-number'
    }

    It 'performs exactly one bind on an authenticated run' {
        Mock Invoke-AdCredentialBind { }
        $secret = ConvertTo-SecureString 'synthetic-test-only' -AsPlainText -Force
        $result = Invoke-AdCredentialCheck -Server localhost -Username example -Password $secret
        $result.Success | Should -BeTrue
        Should -Invoke Invoke-AdCredentialBind -Times 1 -Exactly
        ($result | ConvertTo-Json) | Should -Not -Match 'synthetic-test-only|example"'
    }

    It 'does not retry or leak server exception details when credentials fail' {
        Mock Invoke-AdCredentialBind { throw [System.DirectoryServices.Protocols.LdapException]::new(49, 'sensitive-server-detail') }
        $secret = ConvertTo-SecureString 'synthetic-test-only' -AsPlainText -Force
        $result = Invoke-AdCredentialCheck -Server localhost -Username example -Password $secret
        $result.ExitCode | Should -Be 1
        $result.FailureKind | Should -Be 'Credential'
        Should -Invoke Invoke-AdCredentialBind -Times 1 -Exactly
        ($result | ConvertTo-Json) | Should -Not -Match 'sensitive-server-detail|synthetic-test-only'
    }

    It 'reaches a real localhost TCP listener without binding' {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        try {
            $port = $listener.LocalEndpoint.Port
            $result = Invoke-AdCredentialCheck -Server 127.0.0.1 -Port $port -TestConnectionOnly -TimeoutSeconds 2
            $result.Success | Should -BeTrue
        }
        finally { $listener.Stop() }
    }
}
