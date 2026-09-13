using System.Windows;
using Amber.Tint;

namespace Amber;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Never leave the screen tinted if the app crashes.
        DispatcherUnhandledException += (_, _) => ScreenTint.Reset();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => ScreenTint.Reset();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ScreenTint.Reset();
        base.OnExit(e);
    }
}
