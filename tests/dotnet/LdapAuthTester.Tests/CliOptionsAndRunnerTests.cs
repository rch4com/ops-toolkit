using LdapAuthTester.Cli;
using LdapAuthTester.Models;
using LdapAuthTester.Services;
using Xunit;

namespace LdapAuthTester.Tests;

public class CliOptionsAndRunnerTests
{
    [Fact]
    public void TryParseWithNoArgsEnablesGuiMode()
    {
        bool ok = CliOptions.TryParse([], out CliOptions options, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(options.LaunchGui);
    }

    [Fact]
    public void TryParseWithValidCliArgsParsesExpectedValues()
    {
        string[] args =
        [
            "--domain", "corp.example.com",
            "--user", "alice",
            "--password-env", "TEST_AD_PWD",
            "--ldap-port", "1389",
            "--ldaps-port", "1636",
            "--target", "LdapsOnly",
            "--method", "LdapConnection",
            "--timeout", "15",
            "--include-user-info",
            "--dry-run",
            "--json"
        ];

        bool ok = CliOptions.TryParse(args, out CliOptions options, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.False(options.LaunchGui);
        Assert.Equal("corp.example.com", options.Domain);
        Assert.Equal("alice", options.UserName);
        Assert.Equal("TEST_AD_PWD", options.PasswordEnvVar);
        Assert.Equal(1389, options.LdapPort);
        Assert.Equal(1636, options.LdapsPort);
        Assert.Equal(AuthTestTarget.LdapsOnly, options.Target);
        Assert.Equal(AuthTestMethod.LdapConnection, options.Method);
        Assert.Equal(15, options.TimeoutSeconds);
        Assert.True(options.IncludeUserInfo);
        Assert.True(options.IsDryRun);
        Assert.True(options.OutputJson);
    }

    [Fact]
    public void TryParseWithShortIncludeUserInfoFlagSetsProperty()
    {
        bool ok = CliOptions.TryParse(["-d", "corp.example.com", "-u", "alice", "-p", "secret", "-i"], out CliOptions options, out string? error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(options.IncludeUserInfo);
    }

    [Fact]
    public void TryParseWithInvalidPortReturnsFalseWithError()
    {
        bool ok = CliOptions.TryParse(["--ldap-port", "70000"], out _, out string? error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains("포트", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncWithoutPasswordWhenNotDryRunReturnsExitUsageError()
    {
        var options = new CliOptions
        {
            Domain = "corp.example.com",
            UserName = "alice",
            IsDryRun = false
        };

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        int exitCode = await CliRunner.RunAsync(options, new AdAuthChecker(), outWriter, errWriter);

        Assert.Equal(CliRunner.ExitUsageError, exitCode);
        Assert.Contains("--password", errWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncWithEnvOnlyAndJsonReturnsExitSuccessAndValidJson()
    {
        var options = new CliOptions
        {
            EnvOnly = true,
            OutputJson = true
        };

        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        int exitCode = await CliRunner.RunAsync(options, new AdAuthChecker(), outWriter, errWriter);

        Assert.Equal(CliRunner.ExitSuccess, exitCode);
        string output = outWriter.ToString();
        Assert.Contains("\"overallSuccess\": true", output, StringComparison.Ordinal);
        Assert.Contains("\"environment\":", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsyncWithCertificateAndUserProfilePrintsDiagnosticSections()
    {
        var options = new CliOptions
        {
            Domain = "corp.example.com",
            UserName = "alice",
            Password = "secret",
            Target = AuthTestTarget.LdapsOnly,
            IncludeUserInfo = true
        };

        var fakeChecker = new StubProfileAndCertChecker();
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        int exitCode = await CliRunner.RunAsync(options, fakeChecker, outWriter, errWriter);

        Assert.Equal(CliRunner.ExitSuccess, exitCode);
        string output = outWriter.ToString();
        Assert.Contains("[LDAPS 인증서]", output, StringComparison.Ordinal);
        Assert.Contains("CN=dc01.corp.example.com", output, StringComparison.Ordinal);
        Assert.Contains("[사용자 프로필]", output, StringComparison.Ordinal);
        Assert.Contains("alice@corp.example.com", output, StringComparison.Ordinal);
        Assert.Contains("CN=Domain Admins,CN=Users,DC=corp,DC=example,DC=com", output, StringComparison.Ordinal);
    }

    private sealed class StubProfileAndCertChecker : AdAuthChecker
    {
        public override AdAuthTestResult ExecuteSingleProtocol(AuthProtocol protocol, AdAuthTestRequest request)
        {
            return new AdAuthTestResult
            {
                Protocol = protocol,
                Endpoint = $"LDAP://{request.Domain}:{request.LdapsPort}",
                IsSuccess = true,
                ElapsedMilliseconds = 15,
                DiagnosticDetail = "LDAPS 인증 성공",
                CertificateInfo = new LdapsCertificateInfo
                {
                    Subject = "CN=dc01.corp.example.com",
                    Issuer = "CN=CorpRootCA",
                    Thumbprint = "ABCDEF1234567890",
                    NotBefore = DateTimeOffset.UtcNow.AddDays(-10),
                    NotAfter = DateTimeOffset.UtcNow.AddDays(90),
                    IsExpired = false,
                    DaysUntilExpiration = 90,
                    SslPolicyErrors = "None"
                },
                UserProfile = new AdUserProfileInfo
                {
                    DistinguishedName = "CN=Alice,OU=IT,DC=corp,DC=example,DC=com",
                    SamAccountName = "alice",
                    UserPrincipalName = "alice@corp.example.com",
                    DisplayName = "Alice Kim",
                    Email = "alice@corp.example.com",
                    Department = "Platform Engineering",
                    Title = "Senior Engineer",
                    MemberOfGroups = ["CN=Domain Admins,CN=Users,DC=corp,DC=example,DC=com"]
                }
            };
        }
    }
}
