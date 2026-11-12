using System.Text;
using MIPLabelCustomCheck.Explorer.Cli;

namespace MIPLabelCustomCheck.Explorer;

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
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var checker = new MipLabelChecker();
        return CliRunner.RunAsync(options, checker, Console.Out, Console.Error, cts.Token).GetAwaiter().GetResult();
    }
}
