using System.Globalization;
using LdapAuthTester.Models;

namespace LdapAuthTester.Cli;

public sealed class CliOptions
{
    public bool LaunchGui { get; set; }
    public bool ShowHelp { get; set; }
    public bool ShowVersion { get; set; }
    public bool EnvOnly { get; set; }
    public bool IsDryRun { get; set; }
    public bool OutputJson { get; set; }
    public bool Verbose { get; set; }
    public bool NoLockoutGuard { get; set; }
    public bool IncludeUserInfo { get; set; }
    public string? Domain { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? PasswordEnvVar { get; set; }
    public int LdapPort { get; set; } = 389;
    public int LdapsPort { get; set; } = 636;
    public int TimeoutSeconds { get; set; } = 10;
    public AuthTestTarget Target { get; set; } = AuthTestTarget.Both;
    public AuthTestMethod Method { get; set; } = AuthTestMethod.DirectoryEntry;

    public static bool TryParse(string[] args, out CliOptions options, out string? errorMessage)
    {
        options = new CliOptions();
        errorMessage = null;

        if (args is null || args.Length == 0)
        {
            options.LaunchGui = true;
            return true;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--gui":
                    options.LaunchGui = true;
                    break;

                case "--help":
                case "-h":
                case "/?":
                    options.ShowHelp = true;
                    return true;

                case "--version":
                    options.ShowVersion = true;
                    return true;

                case "--env-only":
                    options.EnvOnly = true;
                    break;

                case "--dry-run":
                    options.IsDryRun = true;
                    break;

                case "--json":
                    options.OutputJson = true;
                    break;

                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;

                case "--no-lockout-guard":
                    options.NoLockoutGuard = true;
                    break;

                case "--include-user-info":
                case "-i":
                    options.IncludeUserInfo = true;
                    break;

                case "--domain":
                case "-d":
                    if (!TryReadValue(args, ref i, arg, out string? domain, out errorMessage))
                    {
                        return false;
                    }
                    options.Domain = domain;
                    break;

                case "--user":
                case "-u":
                    if (!TryReadValue(args, ref i, arg, out string? user, out errorMessage))
                    {
                        return false;
                    }
                    options.UserName = user;
                    break;

                case "--password":
                case "-p":
                    if (!TryReadValue(args, ref i, arg, out string? password, out errorMessage))
                    {
                        return false;
                    }
                    options.Password = password;
                    break;

                case "--password-env":
                    if (!TryReadValue(args, ref i, arg, out string? passwordEnv, out errorMessage))
                    {
                        return false;
                    }
                    options.PasswordEnvVar = passwordEnv;
                    break;

                case "--ldap-port":
                    if (!TryReadPort(args, ref i, arg, out int ldapPort, out errorMessage))
                    {
                        return false;
                    }
                    options.LdapPort = ldapPort;
                    break;

                case "--ldaps-port":
                    if (!TryReadPort(args, ref i, arg, out int ldapsPort, out errorMessage))
                    {
                        return false;
                    }
                    options.LdapsPort = ldapsPort;
                    break;

                case "--timeout":
                    if (!TryReadValue(args, ref i, arg, out string? timeoutStr, out errorMessage))
                    {
                        return false;
                    }
                    if (!int.TryParse(timeoutStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeout) || timeout < 1 || timeout > 300)
                    {
                        errorMessage = "--timeout 값은 1에서 300 사이의 정수(초)여야 합니다.";
                        return false;
                    }
                    options.TimeoutSeconds = timeout;
                    break;

                case "--target":
                case "-t":
                    if (!TryReadValue(args, ref i, arg, out string? targetStr, out errorMessage))
                    {
                        return false;
                    }
                    if (!TryParseTarget(targetStr!, out AuthTestTarget parsedTarget))
                    {
                        errorMessage = $"지원하지 않는 --target 값입니다: '{targetStr}'. (허용값: Both, LdapOnly, LdapsOnly)";
                        return false;
                    }
                    options.Target = parsedTarget;
                    break;

                case "--method":
                case "-m":
                    if (!TryReadValue(args, ref i, arg, out string? methodStr, out errorMessage))
                    {
                        return false;
                    }
                    if (!TryParseMethod(methodStr!, out AuthTestMethod parsedMethod))
                    {
                        errorMessage = $"지원하지 않는 --method 값입니다: '{methodStr}'. (허용값: DirectoryEntry, PrincipalContext, LdapConnection)";
                        return false;
                    }
                    options.Method = parsedMethod;
                    break;

                default:
                    errorMessage = $"알 수 없는 옵션입니다: '{arg}'. 사용법은 --help를 참조하세요.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, string optionName, out string? value, out string? errorMessage)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            value = null;
            errorMessage = $"'{optionName}' 옵션에 값이 필요합니다.";
            return false;
        }

        index++;
        value = args[index];
        errorMessage = null;
        return true;
    }

    private static bool TryReadPort(string[] args, ref int index, string optionName, out int port, out string? errorMessage)
    {
        port = 0;
        if (!TryReadValue(args, ref index, optionName, out string? rawPort, out errorMessage))
        {
            return false;
        }

        if (!int.TryParse(rawPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
        {
            errorMessage = $"'{optionName}' 값은 1에서 65535 사이의 포트 번호여야 합니다.";
            return false;
        }

        return true;
    }

    private static bool TryParseTarget(string raw, out AuthTestTarget target)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "both":
                target = AuthTestTarget.Both;
                return true;
            case "ldap":
            case "ldaponly":
                target = AuthTestTarget.LdapOnly;
                return true;
            case "ldaps":
            case "ldapsonly":
                target = AuthTestTarget.LdapsOnly;
                return true;
            default:
                target = AuthTestTarget.Both;
                return false;
        }
    }

    private static bool TryParseMethod(string raw, out AuthTestMethod method)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "directoryentry":
                method = AuthTestMethod.DirectoryEntry;
                return true;
            case "principalcontext":
                method = AuthTestMethod.PrincipalContext;
                return true;
            case "ldapconnection":
                method = AuthTestMethod.LdapConnection;
                return true;
            default:
                method = AuthTestMethod.DirectoryEntry;
                return false;
        }
    }
}
