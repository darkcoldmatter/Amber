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

    static readonly TimeSpan IdleTick = TimeSpan.FromSeconds(5);

    readonly Settings _settings = Settings.Load(Settings.DefaultPath);
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer _scheduleTimer = new();
    Mutex? _singleInstance;
    bool _previewing;
    TintMode? _previewMode;
    double _previewLevel;
    readonly TintPlan _plan = new();
    float[]? _shown;
    DateTime _lastFadeStep = DateTime.UtcNow;
    TrayIcon? _tray;
    MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TextLook.MakeDefault();
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
        _tray.ToggleRequested += () =>
        {
            if (_settings.IsAdvanced) _settings.AdvancedIsOn = !_settings.AdvancedIsOn;
            else _settings.IsOn = !_settings.IsOn;
        };
        _tray.QuitRequested += () => Shutdown();

        _saveTimer.Tick += (_, _) => SaveSettings();
        _scheduleTimer.Tick += (_, _) => ApplyTint();
        _settings.PropertyChanged += (_, e) =>
        {
            // Only one mode runs at a time: switching one on switches the other off.
            if (e.PropertyName == nameof(Settings.IsOn) && _settings.IsOn) _settings.AdvancedIsOn = false;
            if (e.PropertyName == nameof(Settings.AdvancedIsOn) && _settings.AdvancedIsOn) _settings.IsOn = false;

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

    /// <summary>
    /// While the user drags a level, the screen shows that tint whatever the on/off state.
    /// Advanced cards pass their own mode and level; simple mode uses the screen's.
    /// </summary>
    public void StartPreview(TintMode? mode = null, double level = 0)
    {
        _previewing = true;
        _previewMode = mode;
        _previewLevel = level;
        ApplyTint();
    }

    public void UpdatePreview(TintMode mode, double level)
    {
        if (!_previewing) return;
        _previewMode = mode;
        _previewLevel = level;
        ApplyTint();
    }

    public void EndPreview()
    {
        _previewing = false;
        _previewMode = null;
        _plan.SnapTo(TintNow());   // straight back to the real state, no leftover preview tint
        ApplyTint();
    }

    /// <summary>
    /// Shows the tint the settings ask for right now: the level being previewed, the plain level,
    /// or what the schedule says at this moment (0 outside its hours).
    /// </summary>
    void ApplyTint()
    {
        var wanted = Faded(TintNow());
        if (Changed(wanted))
        {
            if (wanted is null) ScreenTint.Reset();
            else ScreenTint.Apply(wanted);
            _shown = wanted;
        }

        _tray?.SetOn(IsOn);
        RunScheduleTimer();
    }

    /// <summary>Whether the mode the user is in is switched on.</summary>
    bool IsOn => _settings.IsAdvanced ? _settings.AdvancedIsOn : _settings.IsOn;

    /// <summary>The color matrix the screen should show right now, or null for no tint.</summary>
    float[]? TintNow()
    {
        if (_previewing)
            return TintMatrix.Build(_previewMode ?? _settings.Mode, _previewMode is null ? _settings.Level : _previewLevel);

        if (!IsOn) return null;

        if (_settings.IsAdvanced)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            var showing = AdvancedSchedule.Weights(_settings.Filters, now);
            if (showing.Count == 0) return null;
            return TintMatrix.Mix(showing.Select(s =>
                (TintMatrix.Build(s.Filter.Mode, AdvancedSchedule.LevelAt(s.Filter, now)), s.Weight)));
        }

        if (!_settings.ScheduleEnabled) return TintMatrix.Build(_settings.Mode, _settings.Level);

        var strength = Schedule.StrengthAt(TimeOnly.FromDateTime(DateTime.Now),
            _settings.ScheduleStart, _settings.ScheduleEnd, _settings.TransitionSeconds);
        return strength > 0 ? TintMatrix.Blend(_settings.Mode, _settings.Level, strength) : null;
    }

    float[]? Faded(float[]? target)
    {
        var now = DateTime.UtcNow;
        var seconds = (now - _lastFadeStep).TotalSeconds;
        _lastFadeStep = now;
        return _plan.Next(target, eased: !_previewing, seconds);
    }

    /// <summary>Skips tiny changes, so a slow fade doesn't hammer the display pipeline.</summary>
    bool Changed(float[]? wanted)
    {
        if (wanted is null || _shown is null) return wanted is not null || _shown is not null;
        for (var k = 0; k < wanted.Length; k++)
            if (Math.Abs(wanted[k] - _shown[k]) >= 0.002f) return true;
        return false;
    }

    /// <summary>Checks often while a fade is running, rarely when the tint is steady, and not at all without a schedule.</summary>
    void RunScheduleTimer()
    {
        // Keep ticking while a fade finishes, even after the mode was switched off.
        var easing = !_previewing && _plan.IsFading;
        var scheduled = IsOn && !_previewing && (_settings.IsAdvanced ? _settings.Filters.Count > 0 : _settings.ScheduleEnabled);
        if (!scheduled && !easing)
        {
            _scheduleTimer.Stop();
            return;
        }

        // Look one idle check ahead, so the timer is already ticking often when a fade begins.
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var fading = easing || (_settings.IsAdvanced
            ? AdvancedSchedule.IsFading(_settings.Filters, now, IdleTick)
            : Schedule.IsFading(now, _settings.ScheduleStart, _settings.ScheduleEnd, _settings.TransitionSeconds, IdleTick));
        var fadeSeconds = easing ? Fade.DefaultSeconds : _settings.IsAdvanced ? AdvancedSchedule.FadeSeconds : _settings.TransitionSeconds;
        _scheduleTimer.Interval = fading ? Fade.Tick(fadeSeconds) : IdleTick;
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
