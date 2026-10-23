using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Amber.Views;

public partial class SettingsView : UserControl
{
    readonly Settings _settings;
    readonly Updates _updates;
    readonly GlobalHotkeys _hotkeys;
    readonly Dictionary<ShortcutAction, (Button Pill, TextBlock Note)> _rows = [];
    // The shortcut whose pill is waiting for keys, and why the last press for it was refused (shown under its row).
    ShortcutAction? _listening;
    string? _refusal;

    public SettingsView(Settings settings, Updates updates, GlobalHotkeys hotkeys)
    {
        InitializeComponent();
        _settings = settings;
        _updates = updates;
        _hotkeys = hotkeys;
        StartWithWindowsSwitch.IsChecked = StartupRegistration.IsEnabled;
        foreach (var action in Enum.GetValues<ShortcutAction>()) AddShortcutRow(action);

        // Follow updates and shortcuts while Settings is open; let go when it closes, so a closed window isn't kept alive.
        Loaded += (_, _) =>
        {
            updates.Changed += ShowUpdateRow;
            settings.PropertyChanged += OnSettingChanged;
            hotkeys.Changed += ShowShortcuts;
            ShowUpdateRow();
            ShowShortcuts();
        };
        Unloaded += (_, _) =>
        {
            updates.Changed -= ShowUpdateRow;
            settings.PropertyChanged -= OnSettingChanged;
            hotkeys.Changed -= ShowShortcuts;
            StopListening(); // never leave the shortcuts let go
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public event EventHandler? CloseRequested;

    /// <summary>
    /// The "Check for updates" row, a plain stand-in until it is designed: what the button says, whether it can be
    /// clicked (to check, or to install the version on offer, skipped or not), and a note beside it after a failure.
    /// </summary>
    public static (string Button, bool Enabled, string? Note) UpdateRow(UpdateState state) => state.Stage switch
    {
        UpdateStage.NotInstalled => ("Not installed", false, null),
        UpdateStage.Checking => ("Checking…", false, null),
        UpdateStage.UpToDate => ("Up to date", false, null),
        UpdateStage.Available => ($"Update to {state.Version}", true, state.DownloadFailed ? "Couldn't download the update" : null),
        UpdateStage.Downloading => ($"Downloading {state.Percent}%", false, null),
        UpdateStage.Restarting => ("Restarting…", false, null),
        UpdateStage.CheckFailed => ("Check", true, "Couldn't check for updates"),
        UpdateStage.UpdateFailed => ("Check", true, "Couldn't install the update"),
        _ => ("Check", true, null),
    };

    /// <summary>What a shortcut's pill shows: "Ctrl + Alt + A", or "Not set" for none.</summary>
    public static string PillText(ShortcutSettings shortcuts, ShortcutAction action) => shortcuts.Get(action)?.Display ?? NotSet;

    public const string NotSet = "Not set", Listening = "Press keys…", HeldElsewhere = "In use by another app";

    /// <summary>The note under a shortcut's row: why keys were just refused, else whether another app holds it, else none.</summary>
    public static string? Note(string? refusal, bool enabled, bool failed) =>
        refusal ?? (enabled && failed ? HeldElsewhere : null);

    /// <summary>The message when the pressed keys already belong to another of Amber's shortcuts (refused, not swapped).</summary>
    public static string AlreadyUsed(ShortcutAction other) => $"Already used for {ShortcutSettings.Label(other)}";

    void AddShortcutRow(ShortcutAction action)
    {
        var text = new TextBlock { Style = (Style)FindResource("LabelText"), FontSize = 14, Margin = new(0, -1, 0, 1),
            VerticalAlignment = VerticalAlignment.Center };
        SetText(text, ShortcutSettings.Label(action));
        var label = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        if (ShortcutSettings.PicksTint(action))
        {
            // The tints are their own group, under a divider, each with its dome as in the tint menus (19 px, 10 px to the
            // name); E-ink, which has no menu, has a grey one.
            if (action == ShortcutAction.GoldenHour)
                ShortcutList.Children.Add(new Rectangle { Style = (Style)FindResource("Divider"), Margin = new(0, 4, 0, 4) });
            var accent = ShortcutSettings.TintOf(action) is { } tint ? tint + "Accent" : "EInkAccent";
            label.Children.Add(new Path { Data = (Geometry)FindResource("IconDome"), Fill = (Brush)FindResource(accent),
                Margin = new(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
        }
        label.Children.Add(text);
        var pill = new Button { Style = (Style)FindResource("ShortcutPill"), Margin = new(0, 0, 24, 0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Margin = new(0, -1, 0, 1) } };
        AutomationProperties.SetName(pill, ShortcutSettings.Label(action));
        pill.Click += (_, _) =>
        {
            if (_listening == action) StopListening();
            else StartListening(action);
        };
        pill.LostKeyboardFocus += (_, _) =>
        {
            if (_listening == action) StopListening(); // clicked elsewhere, or the window lost focus
        };
        var note = new TextBlock { Style = (Style)FindResource("CaptionText"), Foreground = (Brush)FindResource("OpenTextBrush"),
            Margin = new(0, -4, 24, 6), HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
        Grid.SetRow(note, 1);

        var row = new Grid { RowDefinitions = { new() { Height = new(44) }, new() { Height = GridLength.Auto } } };
        row.Children.Add(label);
        row.Children.Add(pill);
        row.Children.Add(note);
        ShortcutList.Children.Add(row);
        _rows[action] = (pill, note);
    }

    void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.Shortcuts)) ShowShortcuts();
    }

    /// <summary>The switch, each pill's keys (or "Press keys…") and the note under each row. Off: the list dims and can't be used.</summary>
    void ShowShortcuts()
    {
        var shortcuts = _settings.Shortcuts;
        ShortcutsSwitch.IsChecked = shortcuts.Enabled;
        ShortcutList.IsEnabled = shortcuts.Enabled;
        ShortcutList.Opacity = shortcuts.Enabled ? 1 : 0.4;
        ResetRow.IsEnabled = shortcuts.Enabled;
        ResetRow.Opacity = ShortcutList.Opacity;
        foreach (var (action, (pill, note)) in _rows)
        {
            var listening = _listening == action;
            var text = listening ? Listening : PillText(shortcuts, action);
            var keys = (TextBlock)pill.Content;
            SetText(keys, text);
            keys.Foreground = (Brush)FindResource(text is Listening or NotSet ? "SecondaryBrush" : "InkBrush");
            pill.Tag = listening ? "Listening" : null;
            note.Text = Note(listening ? _refusal : null, shortcuts.Enabled, _hotkeys.Failed.Contains(action));
            note.Visibility = string.IsNullOrEmpty(note.Text) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>The pill waits for keys. Meanwhile every global shortcut is let go, so even Amber's own reach the window.</summary>
    void StartListening(ShortcutAction action)
    {
        _listening = action;
        _refusal = null;
        _hotkeys.Suspended = true;
        _rows[action].Pill.Focus();
        ShowShortcuts();
    }

    void StopListening()
    {
        if (_listening is null) return;
        _listening = null;
        _refusal = null;
        _hotkeys.Suspended = false;
        ShowShortcuts();
    }

    /// <summary>While a pill listens, every key press is the new shortcut's (or Esc, Backspace, Delete); nothing else gets it.</summary>
    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_listening is null) return;
        e.Handled = true;
        if (e.IsRepeat) return;
        // Alt combinations arrive as Key.System; an input method or a dead key hides the real key the same way.
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };
        Take(key, Keyboard.Modifiers);
    }

    void Take(Key key, ModifierKeys modifiers)
    {
        if (_listening is not { } action) return;
        var result = ShortcutCapture.Read(key, modifiers);
        switch (result.Kind)
        {
            case CaptureKind.Cancel:
                StopListening();
                break;
            case CaptureKind.Clear:
                _settings.Shortcuts = _settings.Shortcuts.With(action, null);
                StopListening();
                break;
            case CaptureKind.Refused:
                _refusal = ShortcutCapture.Message(result.Problem);
                ShowShortcuts();
                break;
            case CaptureKind.Done when _settings.Shortcuts.UsedBy(action, result.Keys!) is { } other:
                _refusal = AlreadyUsed(other);
                ShowShortcuts();
                break;
            case CaptureKind.Done:
                _settings.Shortcuts = _settings.Shortcuts.With(action, result.Keys);
                StopListening();
                break;
        }
    }

    void OnShortcutsSwitchClick(object sender, RoutedEventArgs e) =>
        _settings.Shortcuts = _settings.Shortcuts with { Enabled = ShortcutsSwitch.IsChecked == true };

    void OnResetShortcutsClick(object sender, RoutedEventArgs e) => _settings.Shortcuts = new();

    /// <summary>
    /// Plus Jakarta Sans for the words, Onest for the numbers: "Ctrl + Alt + 1", "F12". Also the keys under Simple's tint chips.
    /// </summary>
    public static void SetText(TextBlock block, string text)
    {
        block.Inlines.Clear();
        var at = 0;
        foreach (Match number in NumberPattern().Matches(text))
        {
            if (number.Index > at) block.Inlines.Add(new Run(text[at..number.Index]));
            block.Inlines.Add(new Run(number.Value) { FontFamily = (FontFamily)block.FindResource("NumberFont") });
            at = number.Index + number.Length;
        }
        if (at < text.Length) block.Inlines.Add(new Run(text[at..]));
    }

    // A run of digits: "1", "12".
    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    void OnStartWithWindowsClick(object sender, RoutedEventArgs e) =>
        StartupRegistration.IsEnabled = StartWithWindowsSwitch.IsChecked == true;

    void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_updates.State.Stage == UpdateStage.Available) App.Current.InstallUpdate();
        else _ = App.Current.CheckForUpdates(userAsked: true);
    }

    void ShowUpdateRow()
    {
        var (button, enabled, note) = UpdateRow(_updates.State);
        SetButtonText(button);
        AutomationProperties.SetName(UpdateButton, button);
        UpdateButton.IsEnabled = enabled;
        UpdateNote.Text = note;
        UpdateNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Numbers are set in Onest, as everywhere in Amber: "Update to " + "1.1.0", "Downloading " + "42 %".</summary>
    void SetButtonText(string text)
    {
        UpdateText.Inlines.Clear();
        var number = text.IndexOfAny("0123456789".ToCharArray());
        if (number < 0)
        {
            UpdateText.Inlines.Add(new Run(text));
            return;
        }
        UpdateText.Inlines.Add(new Run(text[..number]));
        UpdateText.Inlines.Add(new Run(text[number..]) { FontFamily = (FontFamily)FindResource("NumberFont") });
    }

    void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
