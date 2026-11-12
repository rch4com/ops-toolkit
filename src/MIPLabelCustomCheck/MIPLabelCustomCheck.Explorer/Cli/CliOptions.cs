namespace MIPLabelCustomCheck.Explorer.Cli;

public sealed class CliOptions
{
    public bool LaunchGui { get; set; }
    public bool ShowHelp { get; set; }
    public bool ShowVersion { get; set; }
    public bool IsDryRun { get; set; }
    public bool Recursive { get; set; }
    public bool AllFiles { get; set; }
    public bool WithDetails { get; set; }
    public bool OutputJson { get; set; }
    public bool Verbose { get; set; }
    public string? TargetPath { get; set; }

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

                case "--dry-run":
                    options.IsDryRun = true;
                    break;

                case "--recursive":
                case "-r":
                    options.Recursive = true;
                    break;

                case "--all-files":
                case "-a":
                    options.AllFiles = true;
                    break;

                case "--details":
                case "-d":
                    options.WithDetails = true;
                    break;

                case "--json":
                    options.OutputJson = true;
                    break;

                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;

                case "--path":
                case "-p":
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        errorMessage = $"'{arg}' 옵션에 파일 또는 폴더 경로 값이 필요합니다.";
                        return false;
                    }
                    i++;
                    options.TargetPath = args[i];
                    break;

                default:
                    errorMessage = $"알 수 없는 옵션입니다: '{arg}'. 사용법은 --help를 참조하세요.";
                    return false;
            }
        }

        return true;
    }
}
