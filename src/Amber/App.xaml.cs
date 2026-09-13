using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Amber.Tint;

namespace Amber;

public partial class App : Application
{
    // Windows starts Amber with this at sign-in: stay in the tray, no window.
    internal const string TrayArgument = "--tray";

    readonly Settings _settings = Settings.Load(Settings.DefaultPath);
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    Mutex? _singleInstance;
    TrayIcon? _tray;
    MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var startInTray = e.Args.Contains(TrayArgument);

        // Only one Amber at a time. A second launch asks the running one to show its window, then exits.
        _singleInstance = new Mutex(true, @"Local\Amber-4f6b2c1e", out var isFirstInstance);
        var showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Amber-4f6b2c1e-show");
        if (!isFirstInstance)
        {
            if (!startInTray)
            {
                AllowSetForegroundWindow(ASFW_ANY);
                showRequest.Set();
            }
            Shutdown();
            return;
        }
        ThreadPool.RegisterWaitForSingleObject(showRequest, (_, _) => Dispatcher.InvokeAsync(ShowWindow), null, Timeout.Infinite, false);

        // Keep the sign-in entry pointing at this exe, in case Amber was moved or updated.
        if (StartupRegistration.IsEnabled) StartupRegistration.IsEnabled = true;

        // Never leave the screen tinted if the app crashes.
        DispatcherUnhandledException += (_, _) => ScreenTint.Reset();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => ScreenTint.Reset();

        _tray = new TrayIcon();
        _tray.OpenRequested += ShowWindow;
        _tray.ToggleRequested += () => _settings.IsOn = !_settings.IsOn;
        _tray.QuitRequested += () => Shutdown();

        _saveTimer.Tick += (_, _) => SaveSettings();
        _settings.PropertyChanged += (_, _) =>
        {
            ApplyTint();
            _saveTimer.Stop(); // Save once changes settle, not on every slider step.
            _saveTimer.Start();
        };
        ApplyTint();

        if (startInTray) ReleaseMemorySoon();
        else ShowWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ScreenTint.Reset();
        if (_saveTimer.IsEnabled) SaveSettings();
        _tray?.Dispose();
        base.OnExit(e);
    }

    void ApplyTint()
    {
        if (_settings.IsOn) ScreenTint.Apply(TintMatrix.Build(_settings.Mode, _settings.Level));
        else ScreenTint.Reset();
        _tray?.SetOn(_settings.IsOn);
    }

    void ShowWindow()
    {
        if (_window is null)
        {
            // Closing the window frees it; Amber keeps running in the tray.
            _window = new MainWindow(_settings);
            _window.Closed += (_, _) =>
            {
                _window = null;
                ReleaseMemorySoon();
            };
        }
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    void SaveSettings()
    {
        _saveTimer.Stop();
        try
        {
            _settings.Save(Settings.DefaultPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not worth crashing over; the next change tries again.
        }
    }

    /// <summary>
    /// Amber mostly sits idle in the tray, so hand unused memory back to Windows:
    /// free the closed window, then let Windows page out what isn't being used.
    /// </summary>
    async void ReleaseMemorySoon()
    {
        await Task.Delay(TimeSpan.FromSeconds(5)); // Let startup or closing work finish first.
        if (_window is not null) return;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
    }

    const int ASFW_ANY = -1;

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimumSize, nint maximumSize);
}
