using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using LdapAuthTester.Models;
using LdapAuthTester.Services;
using Xunit;

namespace LdapAuthTester.Tests;

[SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "Testing COMException error code parsing from Active Directory ADSI calls.")]
public class AdAuthCheckerTests
{
    [Fact]
    public void PopulateErrorInfoWithData52eIdentifiesCredentialFailure()
    {
        var ex = new COMException("80090308: LdapErr: DSID-0C090446, comment: AcceptSecurityContext error, data 52e, v2580", unchecked((int)0x80072020));
        var result = new AdAuthTestResult { Protocol = AuthProtocol.LDAP };

        AdAuthChecker.PopulateErrorInfo(ex, result);

        Assert.True(result.IsCredentialFailure);
        Assert.Equal("52e", result.LdapDataCode);
        Assert.Contains("자격 증명 오류", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void PopulateErrorInfoWithData775IdentifiesAccountLockout()
    {
        var ex = new COMException("80090308: LdapErr: DSID-0C090446, comment: AcceptSecurityContext error, data 775, v2580", unchecked((int)0x80072020));
        var result = new AdAuthTestResult { Protocol = AuthProtocol.LDAPS };

        AdAuthChecker.PopulateErrorInfo(ex, result);

        Assert.True(result.IsCredentialFailure);
        Assert.Equal("775", result.LdapDataCode);
        Assert.Contains("계정 잠금", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void PopulateErrorInfoWithData532IdentifiesPasswordExpired()
    {
        var ex = new COMException("80090308: LdapErr: DSID-0C090446, comment: AcceptSecurityContext error, data 532, v2580", unchecked((int)0x80072020));
        var result = new AdAuthTestResult { Protocol = AuthProtocol.LDAP };

        AdAuthChecker.PopulateErrorInfo(ex, result);

        Assert.True(result.IsCredentialFailure);
        Assert.Equal("532", result.LdapDataCode);
        Assert.Contains("비밀번호 만료", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void PopulateErrorInfoWithServerNotOperationalIdentifiesNetworkFailure()
    {
        var ex = new COMException("The server is not operational.", unchecked((int)0x8007203A));
        var result = new AdAuthTestResult { Protocol = AuthProtocol.LDAP };

        AdAuthChecker.PopulateErrorInfo(ex, result);

        Assert.True(result.IsNetworkOrServerFailure);
        Assert.False(result.IsCredentialFailure);
        Assert.Contains("서버 연결 실패", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void PopulateErrorInfoWithSslCertificateErrorIdentifiesSslFailure()
    {
        var ex = new COMException("The server does not support the requested SecureSocketsLayer or certificate is invalid.", unchecked((int)0x8007203B));
        var result = new AdAuthTestResult { Protocol = AuthProtocol.LDAPS };

        AdAuthChecker.PopulateErrorInfo(ex, result);

        Assert.True(result.IsNetworkOrServerFailure);
        Assert.False(result.IsCredentialFailure);
        Assert.Contains("SSL/TLS", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void PopulateErrorInfoWithLogonFailureCodeIdentifiesCredentialFailure()
    {
        var ex = new COMException("Logon failure: unknown user name or bad password.", unchecked((int)0x8007052E));
        var result = new AdAuthTestResult { Protocol = AuthProtocol.LDAP };

        AdAuthChecker.PopulateErrorInfo(ex, result);

        Assert.True(result.IsCredentialFailure);
        Assert.Contains("자격 증명 오류", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCurrentEnvironmentInfoDoesNotThrow()
    {
        var checker = new AdAuthChecker();
        AdEnvironmentInfo info = checker.GetCurrentEnvironmentInfo();

        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.DetectionMessage));
        Assert.False(string.IsNullOrWhiteSpace(info.CurrentLogonUser));
    }

    [Fact]
    public async Task RunAuthTestsAsyncWhenFirstFailsWithCredentialsSkipsSecondToPreventLockout()
    {
        var testChecker = new TestableAdAuthChecker(
            ldapResult: new AdAuthTestResult
            {
                Protocol = AuthProtocol.LDAP,
                IsSuccess = false,
                IsCredentialFailure = true,
                LdapDataCode = "52e",
                DiagnosticDetail = "[자격 증명 오류] 잘못된 비밀번호"
            },
            ldapsResult: new AdAuthTestResult
            {
                Protocol = AuthProtocol.LDAPS,
                IsSuccess = true
            });

        var request = new AdAuthTestRequest
        {
            Domain = "test.domain.local",
            UserName = "testUser",
            Password = "wrongPassword",
            TestTarget = AuthTestTarget.Both,
            SkipSecondOnCredentialFailure = true
        };

        List<AdAuthTestResult> results = await testChecker.RunAuthTestsAsync(request);

        Assert.Equal(2, results.Count);
        Assert.Equal(AuthProtocol.LDAP, results[0].Protocol);
        Assert.False(results[0].IsSuccess);
        Assert.True(results[0].IsCredentialFailure);

        Assert.Equal(AuthProtocol.LDAPS, results[1].Protocol);
        Assert.True(results[1].IsSkipped);
        Assert.NotNull(results[1].SkipReason);
        Assert.Contains("자격 증명 오류", results[1].SkipReason, StringComparison.Ordinal);
        Assert.False(testChecker.LdapsExecuted);
    }

    [Fact]
    public async Task RunAuthTestsAsyncWhenFirstFailsWithNetworkDoesNotSkipSecond()
    {
        var testChecker = new TestableAdAuthChecker(
            ldapResult: new AdAuthTestResult
            {
                Protocol = AuthProtocol.LDAP,
                IsSuccess = false,
                IsNetworkOrServerFailure = true,
                IsCredentialFailure = false,
                DiagnosticDetail = "[서버 연결 실패] 포트 미응답"
            },
            ldapsResult: new AdAuthTestResult
            {
                Protocol = AuthProtocol.LDAPS,
                IsSuccess = true,
                DiagnosticDetail = "성공"
            });

        var request = new AdAuthTestRequest
        {
            Domain = "test.domain.local",
            UserName = "testUser",
            Password = "validPassword",
            TestTarget = AuthTestTarget.Both,
            SkipSecondOnCredentialFailure = true
        };

        List<AdAuthTestResult> results = await testChecker.RunAuthTestsAsync(request);

        Assert.Equal(2, results.Count);
        Assert.Equal(AuthProtocol.LDAP, results[0].Protocol);
        Assert.False(results[0].IsSuccess);

        Assert.Equal(AuthProtocol.LDAPS, results[1].Protocol);
        Assert.False(results[1].IsSkipped);
        Assert.True(results[1].IsSuccess);
        Assert.True(testChecker.LdapsExecuted);
    }

    [Fact]
    public async Task ExecuteDryRunTcpProbeAsyncWithLocalListeningPortReturnsSuccessWithoutAuth()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

        var checker = new AdAuthChecker();
        var request = new AdAuthTestRequest
        {
            Domain = "127.0.0.1",
            LdapPort = port,
            TestTarget = AuthTestTarget.LdapOnly,
            TimeoutSeconds = 3,
            IsDryRun = true
        };

        AdAuthTestResult result = await checker.ExecuteDryRunTcpProbeAsync(AuthProtocol.LDAP, request);
        using TcpClient accepted = await acceptTask;

        Assert.True(result.IsDryRun);
        Assert.True(result.IsSuccess);
        Assert.Contains("Dry-Run", result.DiagnosticDetail, StringComparison.Ordinal);
    }

    private sealed class TestableAdAuthChecker(AdAuthTestResult ldapResult, AdAuthTestResult ldapsResult) : AdAuthChecker
    {
        public bool LdapExecuted { get; private set; }
        public bool LdapsExecuted { get; private set; }

        public override AdAuthTestResult ExecuteSingleProtocol(AuthProtocol protocol, AdAuthTestRequest request)
        {
            if (protocol == AuthProtocol.LDAP)
            {
                LdapExecuted = true;
                return ldapResult;
            }

            LdapsExecuted = true;
            return ldapsResult;
        }
    }
}
