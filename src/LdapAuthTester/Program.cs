using System.Text;
using LdapAuthTester.Cli;
using LdapAuthTester.Services;

namespace LdapAuthTester;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (!CliOptions.TryParse(args, out CliOptions options, out string? errorMessage))
        {
            Console.Error.WriteLine($"[인자 오류] {errorMessage}");
            Console.Error.WriteLine();
            CliRunner.WriteHelp(Console.Error);
            return CliRunner.ExitUsageError;
        }

        if (options.LaunchGui)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var mainForm = new MainForm();
            Application.Run(mainForm);
            return CliRunner.ExitSuccess;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var checker = new AdAuthChecker();
        return CliRunner.RunAsync(options, checker, Console.Out, Console.Error, cts.Token).GetAwaiter().GetResult();
    }
}
