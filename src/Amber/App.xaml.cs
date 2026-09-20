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

    static readonly TimeSpan FadeTick = TimeSpan.FromMilliseconds(500), IdleTick = TimeSpan.FromSeconds(15);

    readonly Settings _settings = Settings.Load(Settings.DefaultPath);
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer _scheduleTimer = new();
    Mutex? _singleInstance;
    bool _previewing;
    double _shownStrength = -1, _shownLevel = -1;
    TintMode? _shownMode;
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
        _scheduleTimer.Tick += (_, _) => ApplyTint();
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
        _scheduleTimer.Stop();
        if (_saveTimer.IsEnabled) SaveSettings();
        _tray?.Dispose();
        base.OnExit(e);
    }

    /// <summary>App instance, for views that need to talk to it.</summary>
    public static new App Current => (App)Application.Current;

    /// <summary>While the user drags a level, the screen shows that tint whatever the on/off state.</summary>
    public void StartPreview()
    {
        _previewing = true;
        ApplyTint();
    }

    public void EndPreview()
    {
        _previewing = false;
        ApplyTint();
    }

    /// <summary>
    /// Shows the tint the settings ask for right now: the level being previewed, the plain level,
    /// or what the schedule says at this moment (0 outside its hours).
    /// </summary>
    void ApplyTint()
    {
        var strength = StrengthNow();
        if (Math.Abs(strength - _shownStrength) >= 0.004 || _settings.Level != _shownLevel || _settings.Mode != _shownMode)
        {
            if (strength > 0) ScreenTint.Apply(TintMatrix.Blend(_settings.Mode, _settings.Level, strength));
            else ScreenTint.Reset();
            _shownStrength = strength;
            _shownLevel = _settings.Level;
            _shownMode = _settings.Mode;
        }

        _tray?.SetOn(_settings.IsOn);
        RunScheduleTimer();
    }

    /// <summary>0 = untinted, 1 = the full tint, in between while a scheduled fade runs.</summary>
    double StrengthNow()
    {
        if (_previewing) return 1;
        if (!_settings.IsOn) return 0;
        if (!_settings.ScheduleEnabled) return 1;

        return Schedule.StrengthAt(TimeOnly.FromDateTime(DateTime.Now),
            _settings.ScheduleStart, _settings.ScheduleEnd, _settings.TransitionMinutes);
    }

    /// <summary>Checks often while a fade is running, rarely when the level is steady, and not at all without a schedule.</summary>
    void RunScheduleTimer()
    {
        if (!_settings.IsOn || !_settings.ScheduleEnabled || _previewing)
        {
            _scheduleTimer.Stop();
            return;
        }

        var fading = Schedule.IsFading(TimeOnly.FromDateTime(DateTime.Now),
            _settings.ScheduleStart, _settings.ScheduleEnd, _settings.TransitionMinutes);
        _scheduleTimer.Interval = fading ? FadeTick : IdleTick;
        _scheduleTimer.Start();
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
