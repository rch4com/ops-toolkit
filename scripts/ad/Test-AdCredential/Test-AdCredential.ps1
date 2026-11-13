#Requires -Version 7.0
<#
.SYNOPSIS
    Tests one LDAP/LDAPS credential bind or TCP reachability.
.DESCRIPTION
    Returns structured results with standard exit codes. WhatIf performs no discovery,
    connection or password prompt. Adapted from Utilities ad-credential-checker.
.PARAMETER Server
    Domain controller; auto-detected only during actual execution.
.PARAMETER Username
    Account to authenticate. Not required for TCP-only or WhatIf.
.PARAMETER Password
    SecureString; prompted only when an actual bind is approved.
.PARAMETER UseLdaps
    Enables LDAPS and defaults the port to 636 instead of 389.
.PARAMETER Port
    Explicit port (1-65535), or 0 for the protocol default.
.PARAMETER TimeoutSeconds
    Connection and bind timeout in seconds, from 1 to 300.
.PARAMETER TestConnectionOnly
    Checks TCP reachability without credentials or bind.
.PARAMETER SkipCertificateCheck
    Explicit per-run LDAPS certificate bypass for diagnostics.
.EXAMPLE
    ./Test-AdCredential.ps1 -Server dc.example.test -Username operator -WhatIf
.EXAMPLE
    ./Test-AdCredential.ps1 -Server localhost -Port 389 -TestConnectionOnly
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$Server = '',
    [string]$Username = '',
    [SecureString]$Password,
    [switch]$UseLdaps,
    [int]$Port = 0,
    [int]$TimeoutSeconds = 15,
    [switch]$TestConnectionOnly,
    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-AdEndpoint {
    [CmdletBinding()]
    param([string]$Server, [int]$Port, [bool]$UseLdaps)

    if ([string]::IsNullOrWhiteSpace($Server)) { $Server = $env:USERDNSDOMAIN }
    if ([string]::IsNullOrWhiteSpace($Server)) {
        $domain = [System.DirectoryServices.ActiveDirectory.Domain]::GetCurrentDomain()
        try { $Server = $domain.Name }
        finally { $domain.Dispose() }
    }
    if ([string]::IsNullOrWhiteSpace($Server)) {
        throw [System.ArgumentException]::new('Specify a domain controller with -Server.')
    }
    if ($Port -eq 0) { $Port = if ($UseLdaps) { 636 } else { 389 } }
    [PSCustomObject]@{ Server = $Server.Trim(); Port = $Port }
}

function Test-AdTcpConnection {
    [CmdletBinding()]
    param([string]$Server, [int]$Port, [int]$TimeoutSeconds)

    $client = [System.Net.Sockets.TcpClient]::new()
    $cancellation = [System.Threading.CancellationTokenSource]::new()
    try {
        $cancellation.CancelAfter([TimeSpan]::FromSeconds($TimeoutSeconds))
        $client.ConnectAsync($Server, $Port, $cancellation.Token).AsTask().GetAwaiter().GetResult()
        return $true
    }
    catch { return $false }
    finally {
        $client.Dispose()
        $cancellation.Dispose()
    }
}

function Get-AdFailureKind {
    [CmdletBinding()]
    param([System.Exception]$Exception)

    while ($null -ne $Exception.InnerException) { $Exception = $Exception.InnerException }
    if ($Exception -is [System.DirectoryServices.Protocols.LdapException]) {
        if ($Exception.ErrorCode -eq 49) { return 'Credential' }
        if ($Exception.ErrorCode -in 81, 85, 91) { return 'Connection' }
    }
    if ($Exception -is [System.Security.Authentication.AuthenticationException]) { return 'Tls' }
    if ($Exception -is [System.Net.Sockets.SocketException] -or
        $Exception -is [System.TimeoutException] -or
        $Exception -is [System.OperationCanceledException]) { return 'Connection' }
    if ($Exception -is [System.ArgumentException]) { return 'Usage' }
    return 'Runtime'
}

function Invoke-AdCredentialBind {
    [CmdletBinding()]
    param([string]$Server, [int]$Port, [bool]$UseLdaps, [string]$Username,
        [SecureString]$Password, [int]$TimeoutSeconds, [bool]$SkipCertificateCheck)

    $credential = [System.Net.NetworkCredential]::new($Username, $Password)
    $identifier = [System.DirectoryServices.Protocols.LdapDirectoryIdentifier]::new($Server, $Port)
    $connection = [System.DirectoryServices.Protocols.LdapConnection]::new($identifier, $credential)
    try {
        $connection.AuthType = [System.DirectoryServices.Protocols.AuthType]::Negotiate
        $connection.SessionOptions.ProtocolVersion = 3
        $connection.SessionOptions.SecureSocketLayer = $UseLdaps
        $connection.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)
        if ($SkipCertificateCheck) {
            Write-Warning 'LDAPS certificate validation is disabled for this run.'
            $connection.SessionOptions.VerifyServerCertificate = { $true }
        }
        $connection.Bind()
    }
    finally { $connection.Dispose() }
}

function Invoke-AdCredentialCheck {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param(
        [string]$Server = '', [string]$Username = '', [SecureString]$Password,
        [switch]$UseLdaps, [int]$Port = 0, [int]$TimeoutSeconds = 15,
        [switch]$TestConnectionOnly, [switch]$SkipCertificateCheck
    )

    $result = [PSCustomObject]@{
        Success = $false; Status = 'Failed'; FailureKind = 'None'; ExitCode = 1
        Server = $Server; Port = $(if ($Port -eq 0) { if ($UseLdaps) { 636 } else { 389 } } else { $Port })
        Protocol = $(if ($UseLdaps) { 'LDAPS' } else { 'LDAP' }); Message = ''
    }
    try {
        if ($Port -lt 0 -or $Port -gt 65535 -or $TimeoutSeconds -lt 1 -or $TimeoutSeconds -gt 300) {
            throw [System.ArgumentException]::new('Port must be 0-65535 and TimeoutSeconds 1-300.')
        }
        if ($SkipCertificateCheck -and -not $UseLdaps) {
            throw [System.ArgumentException]::new('SkipCertificateCheck requires UseLdaps.')
        }
        if (-not $TestConnectionOnly -and [string]::IsNullOrWhiteSpace($Username) -and -not $WhatIfPreference) {
            throw [System.ArgumentException]::new('Username is required for authentication.')
        }
        $action = if ($TestConnectionOnly) { 'Test TCP reachability' } else { 'Attempt one credential bind' }
        $target = if ([string]::IsNullOrWhiteSpace($Server)) { 'Automatically detected domain controller' } else { "${Server}:$($result.Port)" }
        if (-not $PSCmdlet.ShouldProcess($target, $action)) {
            $result.Status = if ($WhatIfPreference) { 'WhatIf' } else { 'Skipped' }
            $result.ExitCode = 0
            $result.Message = $action
            return $result
        }
        $endpoint = Resolve-AdEndpoint -Server $Server -Port $Port -UseLdaps ([bool]$UseLdaps)
        $result.Server = $endpoint.Server
        $result.Port = $endpoint.Port
        if ($TestConnectionOnly) {
            if (-not (Test-AdTcpConnection -Server $endpoint.Server -Port $endpoint.Port -TimeoutSeconds $TimeoutSeconds)) {
                $result.FailureKind = 'Connection'
                $result.Message = 'TCP connection failed; check the server, port and firewall.'
                return $result
            }
        }
        else {
            if ($null -eq $Password) { $Password = Read-Host -Prompt 'Password' -AsSecureString }
            if ($Password.Length -eq 0) { throw [System.ArgumentException]::new('Password must not be empty.') }
            Invoke-AdCredentialBind -Server $endpoint.Server -Port $endpoint.Port -UseLdaps ([bool]$UseLdaps) `
                -Username $Username -Password $Password -TimeoutSeconds $TimeoutSeconds -SkipCertificateCheck ([bool]$SkipCertificateCheck)
        }
        $result.Success = $true
        $result.Status = 'Succeeded'
        $result.ExitCode = 0
        $result.Message = if ($TestConnectionOnly) { 'TCP endpoint is reachable; no authentication attempted.' } else { 'Authentication succeeded.' }
    }
    catch {
        $result.FailureKind = Get-AdFailureKind -Exception $_.Exception
        $result.ExitCode = if ($result.FailureKind -eq 'Usage') { 2 } else { 1 }
        # Return controlled text, because server exception messages can contain account data.
        $result.Message = switch ($result.FailureKind) {
            'Usage' { 'Invalid input; check Username, Password, Port, TimeoutSeconds and LDAPS bypass options.' }
            'Credential' { 'Credentials were rejected; no retry was attempted.' }
            'Connection' { 'Connection failed; check the server, port and firewall.' }
            'Tls' { 'TLS validation failed; check server identity and trusted certificate authorities.' }
            default { 'Diagnostic failed; verify the domain controller and local platform support.' }
        }
    }
    return $result
}

if ($MyInvocation.InvocationName -ne '.') {
    $result = Invoke-AdCredentialCheck @PSBoundParameters
    $result
    exit $result.ExitCode
}
