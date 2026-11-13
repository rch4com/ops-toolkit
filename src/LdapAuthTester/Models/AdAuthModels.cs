using System.Text.Json.Serialization;

namespace LdapAuthTester.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthProtocol
{
    LDAP = 0,
    LDAPS = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthTestMethod
{
    DirectoryEntry = 0,
    PrincipalContext = 1,
    LdapConnection = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthTestTarget
{
    Both = 0,
    LdapOnly = 1,
    LdapsOnly = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthFailureCategory { None, Credential, NetworkOrServer, Tls, Timeout, Unknown, Skipped }

public sealed class AdEnvironmentInfo
{
    public bool IsDomainJoined { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public string DomainController { get; set; } = string.Empty;
    public string CurrentLogonUser { get; set; } = string.Empty;
    public string CurrentNetBiosUser { get; set; } = string.Empty;
    public string DetectionMessage { get; set; } = string.Empty;
}

public sealed class AdAuthTestRequest
{
    public string Domain { get; set; } = string.Empty;
    public int LdapPort { get; set; } = 389;
    public int LdapsPort { get; set; } = 636;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AuthTestMethod TestMethod { get; set; } = AuthTestMethod.DirectoryEntry;
    public AuthTestTarget TestTarget { get; set; } = AuthTestTarget.Both;
    public bool SkipSecondOnCredentialFailure { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
    public bool IsDryRun { get; set; }
    public bool IncludeUserInfo { get; set; }
    public bool SkipCertificateValidation { get; set; }

    public static string? GetCertificateValidationError(bool bypass, AuthTestMethod method, AuthTestTarget target, bool includeUserInfo)
    {
        return bypass && (method != AuthTestMethod.LdapConnection || target != AuthTestTarget.LdapsOnly || includeUserInfo)
            ? "--skip-certificate-validation requires --method LdapConnection --target LdapsOnly and forbids --include-user-info."
            : null;
    }

}

public sealed class LdapsCertificateInfo
{
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public bool IsExpired { get; set; }
    public int DaysUntilExpiration { get; set; }
    public string SslPolicyErrors { get; set; } = string.Empty;
    public List<string> ChainStatusDetails { get; set; } = [];
}

public sealed class AdUserProfileInfo
{
    public string DistinguishedName { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> MemberOfGroups { get; set; } = [];
}

public sealed class AdAuthTestResult
{
    public AuthProtocol Protocol { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public bool IsDryRun { get; set; }
    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    public AuthFailureCategory FailureCategory { get; set; }
    public bool CertificateValidationBypassed { get; set; }
    public bool IsCredentialFailure { get; set; }
    public bool IsNetworkOrServerFailure { get; set; }
    public int Win32ErrorCode { get; set; }
    public string? LdapDataCode { get; set; }
    public string DiagnosticDetail { get; set; } = string.Empty;
    public string? RawErrorMessage { get; set; }
    public LdapsCertificateInfo? CertificateInfo { get; set; }
    public AdUserProfileInfo? UserProfile { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    public override string ToString()
    {
        if (IsSkipped)
        {
            return $"[{Timestamp:HH:mm:ss.fff}] [{Protocol}] SKIPPED: {SkipReason}";
        }

        string modeTag = IsDryRun ? "[DRY-RUN] " : string.Empty;
        if (IsSuccess)
        {
            return $"[{Timestamp:HH:mm:ss.fff}] [{Protocol}] {modeTag}SUCCESS ({ElapsedMilliseconds}ms) -> Endpoint: {Endpoint}, Msg: {DiagnosticDetail}";
        }

        string codeDisplay = LdapDataCode ?? Win32ErrorCode.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        return $"[{Timestamp:HH:mm:ss.fff}] [{Protocol}] {modeTag}FAILED ({ElapsedMilliseconds}ms) [Code: {codeDisplay}] -> Endpoint: {Endpoint}, Msg: {DiagnosticDetail}";
    }
}

public sealed class AdAuthCliReport
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public bool IsDryRun { get; set; }
    public AdEnvironmentInfo Environment { get; set; } = new();
    public bool OverallSuccess { get; set; }
    public List<AdAuthTestResult> Results { get; set; } = [];
}
