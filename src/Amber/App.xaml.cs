using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Amber.Tint;
using Velopack;

namespace Amber;

public partial class App : Application
{
    // Windows starts Amber with this at sign-in: stay in the tray, no window.
    internal const string TrayArgument = "--tray";

    static readonly TimeSpan IdleTick = TimeSpan.FromSeconds(5);

    readonly Settings _settings = Settings.Load(Settings.DefaultPath);
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer _scheduleTimer = new();
    readonly Updates _updates;
    readonly bool _installed;
    readonly DispatcherTimer _updateTimer = new();
#if DEBUG
    readonly bool _pretending;
    bool _pretendNotes;
#endif
    // "New in this version" bullets waiting for the window to show them; null once they have been closed, or if none.
    IReadOnlyList<string>? _notes;
    Mutex? _singleInstance;
    bool _previewing;
    TintMode? _previewMode;
    double _previewLevel, _previewHue;
    readonly TintPlan _plan = new();
    float[]? _shown;
    DateTime _lastFadeStep = DateTime.UtcNow;
    TrayIcon? _tray;
    GlobalHotkeys? _hotkeys;
    MainWindow? _window;

    [STAThread]
    static void Main()
    {
        // Velopack first. While installing, updating or uninstalling, it starts Amber with arguments of its own; Run()
        // handles them and exits right here, before there is a window, a tray icon or a tint.
        VelopackApp.Build()
            // Uninstalling: take Amber off the sign-in list. The settings file stays, in case Amber is installed again.
            .OnBeforeUninstallFastCallback(_ => StartupRegistration.IsEnabled = false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    // A copy run from a build folder (dotnet run) isn't installed: it shows "Not installed" and never checks or updates.
    public App()
    {
        var server = VelopackServer.ForThisCopy();
        _installed = server is not null;
#if DEBUG
        // dotnet run -- --pretend-update 1.0.1: a pretend version on offer, to preview the update notice (PretendUpdates.cs).
        // Skipping it is remembered in settings of its own, never in the real file.
        if (PretendUpdateServer.FromArgs(Environment.GetCommandLineArgs()) is { } pretend)
        {
            _updates = new(pretend, new Settings());
            _pretending = true;
            return;
        }
#endif
        _updates = new(server, _settings);
    }

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

        // Keep the sign-in entry pointing at this exe, in case Amber was moved; never from a build run from source.
        if (StartupRegistration.ShouldRefresh(_installed, StartupRegistration.IsEnabled)) StartupRegistration.IsEnabled = true;

        // Never leave the screen tinted if the app crashes.
        DispatcherUnhandledException += (_, _) => ScreenTint.Reset();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => ScreenTint.Reset();

        _tray = new TrayIcon();
        _tray.OpenRequested += ShowWindow;
        _tray.ToggleRequested += () => ShortcutCommands.Run(ShortcutAction.Toggle, _settings);
        _tray.QuitRequested += () => Shutdown();
#if DEBUG
        // The e-ink experiment (EInkExperiment): the tray and Simple's chips pick the same look; the ticked one again leaves it.
        _tray.EInkRequested += look =>
        {
            if (!_settings.IsAdvanced) EInkExperiment.Look = look == EInkExperiment.Look ? null : look;
        };
        EInkExperiment.Changed += () =>
        {
            _tray.EInk = EInkExperiment.Look;
            ApplyTint();
        };
#endif

        // Global keyboard shortcuts, working with the window closed too.
        _hotkeys = new GlobalHotkeys();
        _hotkeys.Pressed += binding => ShortcutCommands.Run(binding.Action, _settings);
        _hotkeys.Changed += ShowTrayShortcut;
        UseShortcuts();

        _saveTimer.Tick += (_, _) => SaveSettings();
        _scheduleTimer.Tick += (_, _) => ApplyTint();
        _settings.PropertyChanged += (_, e) =>
        {
            // Only one mode runs at a time: switching one on switches the other off.
            if (e.PropertyName == nameof(Settings.IsOn) && _settings.IsOn) _settings.AdvancedIsOn = false;
            if (e.PropertyName == nameof(Settings.AdvancedIsOn) && _settings.AdvancedIsOn) _settings.IsOn = false;
#if DEBUG
            // The e-ink experiment exists only in Simple: switching to Advanced leaves it.
            if (e.PropertyName == nameof(Settings.IsAdvanced) && _settings.IsAdvanced) EInkExperiment.Look = null;
#endif

            if (e.PropertyName == nameof(Settings.Shortcuts)) UseShortcuts();

            ApplyTint();
            _saveTimer.Stop(); // Save once changes settle, not on every slider step.
            _saveTimer.Start();
        };
        ApplyTint();
        DecideNotes(e.Args);

        _updateTimer.Tick += async (_, _) => await CheckForUpdates(userAsked: false);
        ScheduleUpdateCheck();
#if DEBUG
        if (_pretending) _ = CheckForUpdates(userAsked: false); // the pretend version is on offer at once
#endif

        if (startInTray) ReleaseMemorySoon();
        else ShowWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ScreenTint.Reset();
        _scheduleTimer.Stop();
        if (_saveTimer.IsEnabled) SaveSettings();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }

    /// <summary>App instance, for views that need to talk to it.</summary>
    public static new App Current => (App)Application.Current;

    public Updates Updates => _updates;

    /// <summary>Registers the shortcuts the settings ask for: all that are set while the switch is on, none while it's off.</summary>
    void UseShortcuts()
    {
        var shortcuts = _settings.Shortcuts;
        _hotkeys!.Use(shortcuts.Enabled ? shortcuts.Bindings() : []);
        ShowTrayShortcut();
    }

    /// <summary>The tray menu shows the on/off shortcut while it works: shortcuts on, that one set, and not held by another app.</summary>
    void ShowTrayShortcut()
    {
        var shortcuts = _settings.Shortcuts;
        var works = shortcuts.Enabled && shortcuts.Toggle is not null && !_hotkeys!.Failed.Contains(ShortcutAction.Toggle);
        _tray?.SetToggleShortcut(works ? shortcuts.Toggle!.ToString() : null);
    }

    /// <summary>Looks for a new version now. The timer runs this too: a minute after start, then daily.</summary>
    public async Task CheckForUpdates(bool userAsked)
    {
        _updateTimer.Stop();
        await _updates.CheckAsync(userAsked, DateTime.UtcNow);
        ScheduleUpdateCheck();
        if (_window is null) ReleaseMemorySoon(); // a check from the tray loads the network code: give that memory back
    }

    void ScheduleUpdateCheck()
    {
        if (_updates.NextCheckIn(DateTime.UtcNow) is not { } wait) return; // not installed: nothing to check
        _updateTimer.Interval = wait;
        _updateTimer.Start();
    }

    /// <summary>Downloads the update on offer, then quits so the updater can install it and start the new Amber.</summary>
    public async void InstallUpdate()
    {
        if (!await _updates.DownloadAsync()) return;
#if DEBUG
        if (_pretending) return; // nothing to install: the notice stays at "Restarting…"
#endif

        // Every exit path leaves the screen untinted; the new Amber puts the tint back once it starts.
        ScreenTint.Reset();
        _shown = null;
        // Back to the tray if the window was closed while downloading, otherwise with the window.
        if (_updates.ApplyOnExit(_window is null ? [TrayArgument] : [])) Shutdown();
        else ApplyTint(); // the updater didn't start: carry on, tint as before
    }

    /// <summary>
    /// While the user drags a level or Custom's colour, the screen shows that tint whatever the on/off state.
    /// Advanced cards pass their own mode, level and hue; simple mode uses the screen's.
    /// </summary>
    public void StartPreview(TintMode? mode = null, double level = 0, double hue = 0)
    {
        _previewing = true;
        _previewMode = mode;
        _previewLevel = level;
        _previewHue = hue;
        ApplyTint();
    }

    public void UpdatePreview(TintMode mode, double level, double hue)
    {
        if (!_previewing) return;
        _previewMode = mode;
        _previewLevel = level;
        _previewHue = hue;
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
#if DEBUG
        if (_tray is not null) _tray.EInkAvailable = !_settings.IsAdvanced;
#endif
        RunScheduleTimer();
    }

    /// <summary>Whether the mode the user is in is switched on.</summary>
    bool IsOn => _settings.IsAdvanced ? _settings.AdvancedIsOn : _settings.IsOn;

    /// <summary>The color matrix the screen should show right now, or null for no tint.</summary>
    float[]? TintNow()
    {
        if (_previewing)
        {
#if DEBUG
            // Dragging Simple's slider while an e-ink look is picked: the look with that much amber.
            if (_previewMode is null && EInkExperiment.Look is { } preview) return EInkExperiment.Matrix(preview);
#endif
            return _previewMode is { } mode
                ? TintMatrix.Build(mode, _previewLevel, _previewHue)
                : TintMatrix.Build(_settings.Mode, _settings.Level, _settings.CustomHue);
        }

        if (!IsOn) return null;

        if (_settings.IsAdvanced) return AdvancedSchedule.Matrix(_settings.Filters, TimeOnly.FromDateTime(DateTime.Now));

#if DEBUG
        // An e-ink look replaces Simple's tint, with the schedule and its transition applied just the same.
        if (EInkExperiment.Look is { } look)
        {
            var eink = EInkExperiment.Matrix(look);
            if (!_settings.ScheduleEnabled) return eink;
            var einkStrength = Schedule.StrengthAt(TimeOnly.FromDateTime(DateTime.Now),
                _settings.ScheduleStart, _settings.ScheduleEnd, _settings.TransitionSeconds);
            return einkStrength > 0 ? TintMatrix.Mix([(eink, einkStrength)]) : null;
        }
#endif

        if (!_settings.ScheduleEnabled) return TintMatrix.Build(_settings.Mode, _settings.Level, _settings.CustomHue);

        var strength = Schedule.StrengthAt(TimeOnly.FromDateTime(DateTime.Now),
            _settings.ScheduleStart, _settings.ScheduleEnd, _settings.TransitionSeconds);
        return strength > 0 ? TintMatrix.Blend(_settings.Mode, _settings.Level, strength, _settings.CustomHue) : null;
    }

    float[]? Faded(float[]? target)
    {
        var now = DateTime.UtcNow;
        var seconds = (now - _lastFadeStep).TotalSeconds;
        _lastFadeStep = now;
        return _plan.Next(target, eased: !_previewing, seconds);
    }

    /// <summary>Skips changes too small to see, so a slow fade doesn't hammer the display pipeline.</summary>
    bool Changed(float[]? wanted)
    {
        if (wanted is null || _shown is null) return wanted is not null || _shown is not null;
        return TintMatrix.Differs(wanted, _shown);
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
        // Between fades a sloped curve keeps moving: check often enough that its level changes by at most 1 at a time.
        var steady = _settings.IsAdvanced ? AdvancedSchedule.LevelTick(_settings.Filters, now, IdleTick) : IdleTick;
        _scheduleTimer.Interval = fading ? Fade.Tick(fadeSeconds) : steady;
        _scheduleTimer.Start();
    }

    /// <summary>
    /// After an update, this version's notes show once, the next time the window is open (WhatsNew.Decide). A fresh
    /// install shows nothing: its first settings file already counts them as seen.
    /// </summary>
    void DecideNotes(string[] args)
    {
        var notes = ReleaseNotes.Bundled();
#if DEBUG
        // dotnet run -- --pretend-notes: the bullets of ReleaseNotes.md whatever version it names; closing remembers nothing.
        if (args.Contains(PretendNotes.Argument))
        {
            _pretendNotes = true;
            if (notes.Bullets.Count > 0) _notes = notes.Bullets;
            return;
        }
#endif
        switch (WhatsNew.Decide(_installed, _settings.HadFile, notes, WhatsNew.ThisVersion, _settings.NotesShownVersion))
        {
            case NotesAction.Show:
                _notes = notes.Bullets;
                break;
            case NotesAction.MarkSilently:
                _settings.NotesShownVersion = WhatsNew.ThisVersion;
                break;
        }
    }

    void OnNotesClosed()
    {
        _notes = null;
#if DEBUG
        if (_pretendNotes) return;
#endif
        _settings.NotesShownVersion = WhatsNew.ThisVersion;
    }

    void ShowWindow()
    {
        if (_window is null)
        {
            // Closing the window frees it; Amber keeps running in the tray.
            _window = new MainWindow(_settings, _updates, _hotkeys!);
            _window.Closed += (_, _) =>
            {
                _window = null;
                ReleaseMemorySoon();
            };
        }
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
        if (_notes is not null) _window.ShowNotes(_notes, OnNotesClosed);
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
