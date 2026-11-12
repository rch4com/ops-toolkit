using System.Windows;
using MIPLabelCustomCheck.Explorer.Scanning;
using MIPLabelCustomCheck.Explorer.ViewModels;

namespace MIPLabelCustomCheck.Explorer;

/// <summary>Application entry point and composition root.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // MipLabelChecker 인스턴스 하나를 앱 전체가 공유한다. 이 타입은 상태가 없어
        // 스레드 안전하고, TimedChecker의 워밍업 집합도 이 인스턴스에 붙어 세션 내내 유지된다.
        var library = new MipLabelChecker();
        var checker = new TimedChecker((path, options) => library.Check(path, options));

        new MainWindow { DataContext = new MainViewModel(checker) }.Show();
    }
}
