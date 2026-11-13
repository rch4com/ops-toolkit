using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using LdapAuthTester.Models;

namespace LdapAuthTester.Services;

public partial class AdAuthChecker
{
    private const int LdapInvalidCredentialsCode = 49;
    private const int LdapServerDownCode = 81;

    [GeneratedRegex(@"data\s+([0-9a-fA-F]{3,4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LdapDataSubcodeRegex();

    public virtual AdEnvironmentInfo GetCurrentEnvironmentInfo()
    {
        var info = new AdEnvironmentInfo();

        try
        {
            using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
            info.CurrentLogonUser = windowsIdentity?.Name ?? $"{Environment.UserDomainName}\\{Environment.UserName}";
            info.CurrentNetBiosUser = $"{Environment.UserDomainName}\\{Environment.UserName}";

            try
            {
                using Domain domain = Domain.GetComputerDomain();
                if (domain is not null)
                {
                    info.IsDomainJoined = true;
                    info.DomainName = domain.Name;
                    try
                    {
                        using DomainController dc = domain.FindDomainController();
                        info.DomainController = dc?.Name ?? domain.Name;
                    }
                    catch (Exception)
                    {
                        info.DomainController = domain.Name;
                    }

                    info.DetectionMessage = $"AD 도메인에 조인됨: {domain.Name}";
                    return info;
                }
            }
            catch (Exception)
            {
                // Fallback to network domain name
            }

            string netDomain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            if (!string.IsNullOrWhiteSpace(netDomain))
            {
                info.IsDomainJoined = true;
                info.DomainName = netDomain;
                info.DomainController = netDomain;
                info.DetectionMessage = $"네트워크 도메인 감지됨: {netDomain}";
            }
            else if (!string.IsNullOrWhiteSpace(Environment.UserDomainName) &&
                     !string.Equals(Environment.UserDomainName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                info.IsDomainJoined = true;
                info.DomainName = Environment.UserDomainName;
                info.DomainController = Environment.UserDomainName;
                info.DetectionMessage = $"사용자 도메인 감지됨: {Environment.UserDomainName}";
            }
            else
            {
                info.IsDomainJoined = false;
                info.DomainName = string.Empty;
                info.DomainController = string.Empty;
                info.DetectionMessage = "로컬 워크그룹(도메인 미조인 상태이거나 조인 정보 조회 불가)";
            }
        }
        catch (Exception ex)
        {
            info.IsDomainJoined = false;
            info.DetectionMessage = $"환경 정보 조회 중 오류: {ex.Message}";
        }

        return info;
    }

    public async Task<List<AdAuthTestResult>> RunAuthTestsAsync(
        AdAuthTestRequest request,
        Action<AdAuthTestResult>? onResultCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var results = new List<AdAuthTestResult>();
        var targetProtocols = new List<AuthProtocol>();

        if (request.TestTarget == AuthTestTarget.Both)
        {
            targetProtocols.Add(AuthProtocol.LDAP);
            targetProtocols.Add(AuthProtocol.LDAPS);
        }
        else if (request.TestTarget == AuthTestTarget.LdapOnly)
        {
            targetProtocols.Add(AuthProtocol.LDAP);
        }
        else
        {
            targetProtocols.Add(AuthProtocol.LDAPS);
        }

        bool skipSubsequentDueToCredentialFailure = false;
        string skipReasonMessage = string.Empty;

        foreach (AuthProtocol protocol in targetProtocols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (skipSubsequentDueToCredentialFailure)
            {
                int port = protocol == AuthProtocol.LDAP ? request.LdapPort : request.LdapsPort;
                var skippedResult = new AdAuthTestResult
                {
                    Protocol = protocol,
                    Endpoint = $"LDAP://{request.Domain}:{port}",
                    IsSuccess = false,
                    IsDryRun = request.IsDryRun,
                    IsSkipped = true,
                    FailureCategory = AuthFailureCategory.Skipped,
                    SkipReason = skipReasonMessage,
                    DiagnosticDetail = "AD 계정 잠금(Account Lockout) 방지를 위해 인증 시도를 건너뛰었습니다.",
                    ElapsedMilliseconds = 0
                };
                results.Add(skippedResult);
                onResultCallback?.Invoke(skippedResult);
                continue;
            }

            AdAuthTestResult testResult = request.IsDryRun
                ? await ExecuteDryRunTcpProbeAsync(protocol, request, cancellationToken).ConfigureAwait(false)
                : await Task.Run(() => ExecuteSingleProtocol(protocol, request), cancellationToken).ConfigureAwait(false);

            results.Add(testResult);
            onResultCallback?.Invoke(testResult);

            if (!testResult.IsSuccess &&
                testResult.IsCredentialFailure &&
                request.SkipSecondOnCredentialFailure)
            {
                skipSubsequentDueToCredentialFailure = true;
                skipReasonMessage = $"이전 프로토콜({protocol})에서 자격 증명 오류(잘못된 비밀번호 등)가 발생하여 중복 실패/계정 잠금 방지를 위해 후속 인증을 건너뛰었습니다.";
            }
        }

        return results;
    }

    public virtual async Task<AdAuthTestResult> ExecuteDryRunTcpProbeAsync(
        AuthProtocol protocol,
        AdAuthTestRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        int port = protocol == AuthProtocol.LDAP ? request.LdapPort : request.LdapsPort;
        string endpoint = $"LDAP://{request.Domain}:{port}";

        var result = new AdAuthTestResult
        {
            Protocol = protocol,
            Endpoint = endpoint,
            IsDryRun = true
        };

        var sw = Stopwatch.StartNew();
        int timeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 10;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(request.Domain, port, linkedCts.Token).ConfigureAwait(false);

            sw.Stop();
            result.IsSuccess = true;
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.DiagnosticDetail = $"[Dry-Run] {protocol} TCP 포트({port}) 연결 확인 완료 ({sw.ElapsedMilliseconds} ms) — 실제 AD 인증(Bind)은 생략됨";

            if (protocol == AuthProtocol.LDAPS)
            {
                LdapsCertificateInfo? certInfo = await InspectLdapsCertificateAsync(
                    request.Domain,
                    port,
                    timeoutSeconds,
                    cancellationToken).ConfigureAwait(false);

                if (certInfo is not null)
                {
                    result.CertificateInfo = certInfo;
                    result.DiagnosticDetail += $" | Cert Subject: {certInfo.Subject}, Expires: {certInfo.NotAfter:yyyy-MM-dd} (D-{certInfo.DaysUntilExpiration}), SSL Errors: {certInfo.SslPolicyErrors}";
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            result.IsSuccess = false;
            result.IsNetworkOrServerFailure = true;
            result.FailureCategory = AuthFailureCategory.Timeout;
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.DiagnosticDetail = $"[Dry-Run 연결 타임아웃] {request.Domain}:{port} TCP 연결이 {timeoutSeconds}초 내에 응답하지 않았습니다. 방화벽 및 네트워크 경로를 확인하세요.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            result.IsSuccess = false;
            result.IsNetworkOrServerFailure = true;
            result.FailureCategory = AuthFailureCategory.NetworkOrServer;
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.RawErrorMessage = ex.ToString();
            result.DiagnosticDetail = $"[Dry-Run 포트 연결 실패] {request.Domain}:{port} TCP 연결 실패: {ex.Message}";
        }

        return result;
    }

    [SuppressMessage("Security", "CA5359:Do Not Disable Certificate Validation", Justification = "Diagnostic inspection of remote LDAPS server certificate metadata including self-signed or expired certificates.")]
    public virtual async Task<LdapsCertificateInfo?> InspectLdapsCertificateAsync(
        string host,
        int port,
        int timeoutSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        int effectiveTimeout = timeoutSeconds > 0 ? timeoutSeconds : 10;
        LdapsCertificateInfo? capturedCertInfo = null;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);

            using var sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                (_, certificate, chain, sslPolicyErrors) =>
                {
                    if (certificate is not null)
                    {
                        var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
                        bool disposeCert2 = !ReferenceEquals(cert2, certificate);
                        try
                        {
                            var notBefore = new DateTimeOffset(cert2.NotBefore);
                            var notAfter = new DateTimeOffset(cert2.NotAfter);
                            DateTimeOffset now = DateTimeOffset.Now;

                            var chainDetails = new List<string>();
                            if (chain?.ChainStatus is not null)
                            {
                                foreach (X509ChainStatus status in chain.ChainStatus)
                                {
                                    string infoText = status.StatusInformation?.Trim() ?? string.Empty;
                                    chainDetails.Add(string.IsNullOrEmpty(infoText)
                                        ? status.Status.ToString()
                                        : $"{status.Status}: {infoText}");
                                }
                            }

                            capturedCertInfo = new LdapsCertificateInfo
                            {
                                Subject = cert2.Subject,
                                Issuer = cert2.Issuer,
                                Thumbprint = cert2.Thumbprint,
                                NotBefore = notBefore,
                                NotAfter = notAfter,
                                IsExpired = now > notAfter,
                                DaysUntilExpiration = (int)Math.Floor((notAfter - now).TotalDays),
                                SslPolicyErrors = sslPolicyErrors.ToString(),
                                ChainStatusDetails = chainDetails
                            };
                        }
                        finally
                        {
                            if (disposeCert2)
                            {
                                cert2.Dispose();
                            }
                        }
                    }

                    return true;
                });

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = host
            };

            await sslStream.AuthenticateAsClientAsync(sslOptions, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Best-effort inspection: return captured certificate info if callback was reached
        }

        return capturedCertInfo;
    }

    public virtual AdAuthTestResult ExecuteSingleProtocol(AuthProtocol protocol, AdAuthTestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        if (request.SkipCertificateValidation && protocol != AuthProtocol.LDAPS)
        {
            throw new ArgumentException("Certificate validation bypass requires LDAPS.", nameof(protocol));
        }

        int port = protocol == AuthProtocol.LDAP ? request.LdapPort : request.LdapsPort;
        bool useSsl = protocol == AuthProtocol.LDAPS;
        string endpoint = $"LDAP://{request.Domain}:{port}";

        var result = new AdAuthTestResult
        {
            Protocol = protocol,
            Endpoint = endpoint,
            IsDryRun = false,
            CertificateValidationBypassed = request.SkipCertificateValidation
        };

        var sw = Stopwatch.StartNew();

        try
        {
            switch (request.TestMethod)
            {
                case AuthTestMethod.LdapConnection:
                    ExecuteViaLdapConnection(request.Domain, request.UserName, request.Password, port, useSsl, request.TimeoutSeconds, request.SkipCertificateValidation);
                    break;

                case AuthTestMethod.PrincipalContext:
                    ExecuteViaPrincipalContext(request.Domain, request.UserName, request.Password, port, useSsl);
                    break;

                case AuthTestMethod.DirectoryEntry:
                default:
                    ExecuteViaDirectoryEntry(request.Domain, request.UserName, request.Password, port, useSsl);
                    break;
            }

            sw.Stop();
            result.IsSuccess = true;
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.DiagnosticDetail = $"{protocol} (Port {port}, {request.TestMethod}) 인증 성공 ({sw.ElapsedMilliseconds} ms)";

            if (request.IncludeUserInfo)
            {
                result.UserProfile = TryQueryUserProfile(request.Domain, request.UserName, request.Password, port, useSsl);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.IsSuccess = false;
            result.ElapsedMilliseconds = sw.ElapsedMilliseconds;
            result.RawErrorMessage = ex.ToString();

            PopulateErrorInfo(ex, result);
        }

        if (useSsl && (result.IsSuccess || result.IsNetworkOrServerFailure))
        {
            try
            {
                LdapsCertificateInfo? certInfo = InspectLdapsCertificateAsync(
                    request.Domain,
                    port,
                    request.TimeoutSeconds).GetAwaiter().GetResult();

                if (certInfo is not null)
                {
                    result.CertificateInfo = certInfo;
                    if (!result.IsSuccess)
                    {
                        result.DiagnosticDetail += $" [인증서 진단: Subject='{certInfo.Subject}', 만료일={certInfo.NotAfter:yyyy-MM-dd}, SSL오류={certInfo.SslPolicyErrors}]";
                    }
                }
            }
            catch (Exception)
            {
                // Best-effort certificate inspection
            }
        }

        return result;
    }

    public virtual AdUserProfileInfo? TryQueryUserProfile(string domain, string userName, string password, int port, bool useSsl)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        try
        {
            string path = $"LDAP://{domain}:{port}";
            AuthenticationTypes authTypes = useSsl
                ? AuthenticationTypes.SecureSocketsLayer | AuthenticationTypes.Secure
                : AuthenticationTypes.None;

            using var entry = new DirectoryEntry(path, userName, password, authTypes);
            string normalizedAccount = NormalizeSearchAccountName(userName);
            string escapedAccount = EscapeLdapSearchFilter(normalizedAccount);

            using var searcher = new DirectorySearcher(entry)
            {
                Filter = $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName={escapedAccount})(userPrincipalName={escapedAccount})))",
                SearchScope = System.DirectoryServices.SearchScope.Subtree
            };

            searcher.PropertiesToLoad.AddRange(
            [
                "distinguishedName",
                "sAMAccountName",
                "userPrincipalName",
                "displayName",
                "mail",
                "department",
                "title",
                "memberOf"
            ]);

            System.DirectoryServices.SearchResult? searchResult = searcher.FindOne();
            if (searchResult is null)
            {
                return null;
            }

            var profile = new AdUserProfileInfo
            {
                DistinguishedName = GetFirstStringProperty(searchResult, "distinguishedName"),
                SamAccountName = GetFirstStringProperty(searchResult, "sAMAccountName"),
                UserPrincipalName = GetFirstStringProperty(searchResult, "userPrincipalName"),
                DisplayName = GetFirstStringProperty(searchResult, "displayName"),
                Email = GetFirstStringProperty(searchResult, "mail"),
                Department = GetFirstStringProperty(searchResult, "department"),
                Title = GetFirstStringProperty(searchResult, "title")
            };

            if (searchResult.Properties.Contains("memberOf"))
            {
                ResultPropertyValueCollection values = searchResult.Properties["memberOf"];
                foreach (object? val in values)
                {
                    if (val is string groupDn && !string.IsNullOrWhiteSpace(groupDn))
                    {
                        profile.MemberOfGroups.Add(groupDn);
                    }
                }
            }

            return profile;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string NormalizeSearchAccountName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return string.Empty;
        }

        string trimmed = userName.Trim();
        int slashIndex = trimmed.IndexOf('\\', StringComparison.Ordinal);
        if (slashIndex >= 0 && slashIndex + 1 < trimmed.Length)
        {
            return trimmed[(slashIndex + 1)..].Trim();
        }

        return trimmed;
    }

    public static string EscapeLdapSearchFilter(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\':
                    sb.Append(@"\5c");
                    break;
                case '*':
                    sb.Append(@"\2a");
                    break;
                case '(':
                    sb.Append(@"\28");
                    break;
                case ')':
                    sb.Append(@"\29");
                    break;
                case '\0':
                    sb.Append(@"\00");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string GetFirstStringProperty(System.DirectoryServices.SearchResult result, string propertyName)
    {
        if (!result.Properties.Contains(propertyName))
        {
            return string.Empty;
        }

        ResultPropertyValueCollection values = result.Properties[propertyName];
        if (values.Count == 0 || values[0] is null)
        {
            return string.Empty;
        }

        return values[0]?.ToString() ?? string.Empty;
    }

    private static void ExecuteViaDirectoryEntry(string domain, string userName, string password, int port, bool useSsl)
    {
        string path = $"LDAP://{domain}:{port}";
        AuthenticationTypes authTypes = useSsl
            ? AuthenticationTypes.SecureSocketsLayer | AuthenticationTypes.Secure
            : AuthenticationTypes.None;

        using var entry = new DirectoryEntry(path, userName, password, authTypes);
        object nativeObject = entry.NativeObject;
        if (nativeObject is null)
        {
            throw new InvalidOperationException("DirectoryEntry.NativeObject returned null.");
        }
    }

    private static void ExecuteViaPrincipalContext(string domain, string userName, string password, int port, bool useSsl)
    {
        string serverName = $"{domain}:{port}";
        ContextOptions options = useSsl
            ? ContextOptions.Negotiate | ContextOptions.SecureSocketLayer
            : ContextOptions.Negotiate;

        using var context = new PrincipalContext(ContextType.Domain, serverName, null, options);
        bool isValid = context.ValidateCredentials(userName, password, options);
        if (!isValid)
        {
            throw new UnauthorizedAccessException("PrincipalContext.ValidateCredentials returned false (Invalid credentials).");
        }
    }

    [SuppressMessage("Security", "CA5359:Do Not Disable Certificate Validation", Justification = "Explicit per-run diagnostic opt-in restricted to LDAPS LdapConnection without profile lookup.")]
    private static void ExecuteViaLdapConnection(string domain, string userName, string password, int port, bool useSsl, int timeoutSeconds, bool skipCertificateValidation)
    {
        var identifier = new LdapDirectoryIdentifier(domain, port, fullyQualifiedDnsHostName: false, connectionless: false);
        var credential = new NetworkCredential(userName, password);

        using var connection = new LdapConnection(identifier, credential, AuthType.Negotiate);
        connection.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10);
        connection.SessionOptions.ProtocolVersion = 3;
        if (useSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
            if (skipCertificateValidation)
            {
                connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
            }
        }

        connection.Bind();
    }

    private static void ValidateRequest(AdAuthTestRequest request)
    {
        string? error = AdAuthTestRequest.GetCertificateValidationError(request.SkipCertificateValidation, request.TestMethod, request.TestTarget, request.IncludeUserInfo);
        if (error is not null)
        {
            throw new ArgumentException(error, nameof(request));
        }
    }

    public static void PopulateErrorInfo(Exception ex, AdAuthTestResult result)
    {
        if (ex is null || result is null)
        {
            return;
        }

        if (ex.InnerException is not null)
        {
            PopulateErrorInfo(ex.InnerException, result);
            return;
        }
        if (ex is TimeoutException or SocketException or System.Security.Authentication.AuthenticationException)
        {
            result.IsNetworkOrServerFailure = true;
            result.FailureCategory = ex switch
            {
                TimeoutException => AuthFailureCategory.Timeout,
                System.Security.Authentication.AuthenticationException => AuthFailureCategory.Tls,
                _ => AuthFailureCategory.NetworkOrServer
            };
            result.DiagnosticDetail = $"[{result.FailureCategory}] 연결 또는 TLS 검증 실패";
            return;
        }
        result.FailureCategory = AuthFailureCategory.Unknown;
        string fullMessage = ex.ToString();
        int win32Code = 0;

        if (ex is COMException comEx)
        {
            win32Code = comEx.ErrorCode;
        }
        else if (ex is LdapException ldapEx)
        {
            win32Code = ldapEx.ErrorCode;
            if (!string.IsNullOrWhiteSpace(ldapEx.ServerErrorMessage))
            {
                fullMessage = $"{fullMessage} {ldapEx.ServerErrorMessage}";
            }
        }

        result.Win32ErrorCode = win32Code;

        Match match = LdapDataSubcodeRegex().Match(fullMessage);
        if (match.Success)
        {
            result.LdapDataCode = match.Groups[1].Value.ToLowerInvariant();
        }

        if (result.LdapDataCode is not null)
        {
            switch (result.LdapDataCode)
            {
                case "52e":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[자격 증명 오류] 아이디 또는 비밀번호가 잘못되었습니다 (Data 52e: Logon failure / Bad password).";
                    return;
                case "525":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[사용자 없음] 해당 사용자를 찾을 수 없습니다 (Data 525: User not found).";
                    return;
                case "775":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[계정 잠금] 계정이 잠겨 있습니다 (Data 775: User account locked).";
                    return;
                case "532":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[비밀번호 만료] 비밀번호가 만료되었습니다 (Data 532: Password expired).";
                    return;
                case "533":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[계정 비활성화] 계정이 비활성화되었습니다 (Data 533: Account disabled).";
                    return;
                case "701":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[계정 만료] 계정이 만료되었습니다 (Data 701: Account expired).";
                    return;
                case "773":
                    result.IsCredentialFailure = true;
                    result.FailureCategory = AuthFailureCategory.Credential;
                    result.DiagnosticDetail = "[비밀번호 재설정 필요] 첫 로그인 비밀번호 변경이 필요합니다 (Data 773: User must reset password).";
                    return;
            }
        }

        if (win32Code == unchecked((int)0x8007052E) ||
            win32Code == LdapInvalidCredentialsCode ||
            ex is UnauthorizedAccessException)
        {
            result.IsCredentialFailure = true;
            result.FailureCategory = AuthFailureCategory.Credential;
            result.DiagnosticDetail = "[자격 증명 오류] 사용자 ID 또는 비밀번호가 일치하지 않습니다 (0x8007052E / LDAP 49: Logon failure).";
            return;
        }

        if (win32Code == unchecked((int)0x8007203A) ||
            win32Code == LdapServerDownCode ||
            fullMessage.Contains("The server is not operational", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("서버가 작동하지 않습니다", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("The LDAP server is unavailable", StringComparison.OrdinalIgnoreCase))
        {
            result.IsNetworkOrServerFailure = true;
            result.FailureCategory = AuthFailureCategory.NetworkOrServer;
            result.DiagnosticDetail = "[서버 연결 실패] AD 서버에 연결할 수 없거나 포트가 닫혀 있습니다 (0x8007203A / LDAP 81: Server not operational). 방화벽 및 포트/SSL 인증서를 확인하세요.";
            return;
        }

        if (fullMessage.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("SecureSocketsLayer", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            fullMessage.Contains("인증서", StringComparison.OrdinalIgnoreCase))
        {
            result.IsNetworkOrServerFailure = true;
            result.FailureCategory = AuthFailureCategory.Tls;
            result.DiagnosticDetail = "[SSL/TLS 오류] LDAPS SSL/TLS 핸드셰이크 또는 인증서 검증 실패. 서버 인증서 신뢰 상태 및 포트 636을 확인하세요.";
            return;
        }

        result.DiagnosticDetail = $"[기타 오류] {ex.Message}";
    }
}
