using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using LdapAuthTester.Models;
using LdapAuthTester.Services;

namespace LdapAuthTester.Cli;

public static class CliRunner
{
    public const int ExitSuccess = 0;
    public const int ExitFailure = 1;
    public const int ExitUsageError = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task<int> RunAsync(
        CliOptions options,
        AdAuthChecker checker,
        TextWriter stdOut,
        TextWriter stdErr,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(stdOut);
        ArgumentNullException.ThrowIfNull(stdErr);

        if (options.ShowHelp)
        {
            WriteHelp(stdOut);
            return ExitSuccess;
        }

        if (options.ShowVersion)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            await stdOut.WriteLineAsync($"LdapAuthTester v{version}").ConfigureAwait(false);
            return ExitSuccess;
        }

        AdEnvironmentInfo envInfo = checker.GetCurrentEnvironmentInfo();

        if (options.EnvOnly)
        {
            if (options.OutputJson)
            {
                var envReport = new AdAuthCliReport
                {
                    IsDryRun = options.IsDryRun,
                    Environment = envInfo,
                    OverallSuccess = true,
                    Results = []
                };
                await stdOut.WriteLineAsync(JsonSerializer.Serialize(envReport, JsonOptions)).ConfigureAwait(false);
            }
            else
            {
                WriteEnvironmentBanner(stdOut, envInfo);
            }

            return ExitSuccess;
        }

        string? domain = !string.IsNullOrWhiteSpace(options.Domain)
            ? options.Domain.Trim()
            : envInfo.DomainName;

        if (string.IsNullOrWhiteSpace(domain))
        {
            await stdErr.WriteLineAsync("[오류] 대상 도메인(--domain)이 지정되지 않았으며 현재 PC에서 AD 도메인을 자동 감지할 수 없습니다.").ConfigureAwait(false);
            return ExitUsageError;
        }

        string? password = options.Password;
        if (string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(options.PasswordEnvVar))
        {
            password = Environment.GetEnvironmentVariable(options.PasswordEnvVar);
            if (string.IsNullOrEmpty(password) && !options.IsDryRun)
            {
                await stdErr.WriteLineAsync($"[오류] 지정한 환경 변수 '{options.PasswordEnvVar}'이(가) 비어 있거나 존재하지 않습니다.").ConfigureAwait(false);
                return ExitUsageError;
            }
        }

        if (!options.IsDryRun)
        {
            if (string.IsNullOrWhiteSpace(options.UserName))
            {
                await stdErr.WriteLineAsync("[오류] 실제 AD 인증 테스트 시에는 --user <id> 옵션이 필수입니다. (포트 연결만 점검하려면 --dry-run을 사용하세요)").ConfigureAwait(false);
                return ExitUsageError;
            }

            if (string.IsNullOrEmpty(password))
            {
                await stdErr.WriteLineAsync("[오류] 실제 AD 인증 테스트 시에는 --password 또는 --password-env 옵션이 필수입니다. (포트 연결만 점검하려면 --dry-run을 사용하세요)").ConfigureAwait(false);
                return ExitUsageError;
            }
        }

        var request = new AdAuthTestRequest
        {
            Domain = domain,
            LdapPort = options.LdapPort,
            LdapsPort = options.LdapsPort,
            UserName = options.UserName?.Trim() ?? string.Empty,
            Password = password ?? string.Empty,
            TestTarget = options.Target,
            TestMethod = options.Method,
            SkipSecondOnCredentialFailure = !options.NoLockoutGuard,
            TimeoutSeconds = options.TimeoutSeconds,
            IsDryRun = options.IsDryRun
        };

        try
        {
            List<AdAuthTestResult> results = await checker.RunAuthTestsAsync(request, null, cancellationToken).ConfigureAwait(false);
            bool overallSuccess = results.Count > 0 && results.TrueForAll(r => r.IsSuccess);

            if (options.OutputJson)
            {
                if (!options.Verbose)
                {
                    foreach (AdAuthTestResult r in results)
                    {
                        r.RawErrorMessage = null;
                    }
                }

                var report = new AdAuthCliReport
                {
                    IsDryRun = request.IsDryRun,
                    Environment = envInfo,
                    OverallSuccess = overallSuccess,
                    Results = results
                };

                await stdOut.WriteLineAsync(JsonSerializer.Serialize(report, JsonOptions)).ConfigureAwait(false);
            }
            else
            {
                WriteEnvironmentBanner(stdOut, envInfo);
                await stdOut.WriteLineAsync(new string('-', 72)).ConfigureAwait(false);
                string modeTitle = request.IsDryRun ? "DRY-RUN (사전 TCP 포트 연결 점검)" : $"AD 인증 검증 ({request.TestMethod})";
                await stdOut.WriteLineAsync($"[실행 모드] {modeTitle} | 대상: {request.Domain} (LDAP:{request.LdapPort}, LDAPS:{request.LdapsPort})").ConfigureAwait(false);

                foreach (AdAuthTestResult r in results)
                {
                    await stdOut.WriteLineAsync($"  {r}").ConfigureAwait(false);
                    if (options.Verbose && !string.IsNullOrWhiteSpace(r.RawErrorMessage))
                    {
                        await stdOut.WriteLineAsync($"    [상세 예외] {r.RawErrorMessage}").ConfigureAwait(false);
                    }
                }

                await stdOut.WriteLineAsync(new string('-', 72)).ConfigureAwait(false);
                await stdOut.WriteLineAsync(overallSuccess ? "최종 결과: SUCCESS (정상)" : "최종 결과: FAILED (실패 또는 일부 생략됨)").ConfigureAwait(false);
            }

            return overallSuccess ? ExitSuccess : ExitFailure;
        }
        catch (OperationCanceledException)
        {
            await stdErr.WriteLineAsync("[중단됨] 사용자 요청(Ctrl+C)에 의해 작업이 취소되었습니다.").ConfigureAwait(false);
            return ExitFailure;
        }
        catch (Exception ex)
        {
            await stdErr.WriteLineAsync($"[실행 오류] {ex.Message}").ConfigureAwait(false);
            return ExitFailure;
        }
    }

    private static void WriteEnvironmentBanner(TextWriter writer, AdEnvironmentInfo env)
    {
        writer.WriteLine($"[환경 감지] 도메인 조인: {(env.IsDomainJoined ? "Yes" : "No")} ({env.DetectionMessage})");
        writer.WriteLine($"[현재 계정] {env.CurrentLogonUser} | 감지 도메인: {(string.IsNullOrEmpty(env.DomainName) ? "-" : env.DomainName)} | 감지 DC: {(string.IsNullOrEmpty(env.DomainController) ? "-" : env.DomainController)}");
    }

    public static void WriteHelp(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteLine("LdapAuthTester — Active Directory LDAP(389) / LDAPS(636) 인증 및 연결성 진단 도구");
        writer.WriteLine();
        writer.WriteLine("사용법:");
        writer.WriteLine("  LdapAuthTester                           인자 없이 실행 시 WinForms GUI 실행");
        writer.WriteLine("  LdapAuthTester --gui                     WinForms GUI 명시적 실행");
        writer.WriteLine("  LdapAuthTester --env-only [--json]       현재 PC의 AD 도메인 조인 및 로그인 사용자 상태 조회");
        writer.WriteLine("  LdapAuthTester --domain <host> --dry-run 실제 인증 없이 LDAP/LDAPS TCP 포트 개방 여부만 안전하게 점검");
        writer.WriteLine("  LdapAuthTester --domain <host> --user <id> --password-env <ENV_VAR> [옵션]");
        writer.WriteLine();
        writer.WriteLine("주요 옵션:");
        writer.WriteLine("  -d, --domain <host>        대상 AD 도메인 또는 Domain Controller 호스트/IP (미지정 시 자동 감지)");
        writer.WriteLine("  -u, --user <id>            인증할 AD 사용자 ID (--dry-run 시 불필요)");
        writer.WriteLine("  -p, --password <pw>        사용자 비밀번호 (보안을 위해 --password-env 권장)");
        writer.WriteLine("      --password-env <var>   비밀번호를 읽어올 환경 변수 이름");
        writer.WriteLine("      --ldap-port <port>     LDAP 포트 (기본값: 389)");
        writer.WriteLine("      --ldaps-port <port>    LDAPS SSL 포트 (기본값: 636)");
        writer.WriteLine("  -t, --target <target>      테스트 대상: Both (기본값), LdapOnly, LdapsOnly");
        writer.WriteLine("  -m, --method <method>      검증 방식: DirectoryEntry (기본값), PrincipalContext, LdapConnection");
        writer.WriteLine("      --timeout <sec>        연결 타임아웃 초 (기본값: 10)");
        writer.WriteLine("      --no-lockout-guard     1차 프로토콜 자격 증명 실패 시에도 2차 프로토콜 강제 시도");
        writer.WriteLine("      --dry-run              실제 AD 인증을 생략하고 도메인 감지 및 TCP 포트 연결성만 점검");
        writer.WriteLine("      --env-only             도메인 조인 상태 및 현재 사용자 환경만 출력 후 종료");
        writer.WriteLine("      --json                 결과를 JSON 포맷으로 출력");
        writer.WriteLine("  -v, --verbose              실패 시 원본 예외 메시지 상세 출력");
        writer.WriteLine("  -h, --help                 도움말 출력");
        writer.WriteLine("      --version              버전 출력");
    }
}
