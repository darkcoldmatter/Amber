using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Amber.Views;

/// <summary>
/// Announces a new version: the note "A new version is available" with Update and ✕ (skip this version). Update
/// downloads it, the button counting up in place meanwhile and ✕ hidden, and then Amber restarts into it; if the download
/// fails, the note says so and Update can be tried again. Shows only while <see cref="Updates"/> has a version to
/// announce, or is downloading or restarting into one.
/// </summary>
public partial class UpdateNotice : UserControl
{
    // Stacked, the buttons start 9 px below the note's line; in a row, the note keeps 16 px from them.
    const double StackedGap = 9, RowGap = 16;

    Updates? _updates;
    bool _stacked;

    public UpdateNotice()
    {
        InitializeComponent();
        Arrange();

        // Follow the updates while in the window; let go when out of it, so a closed window can be freed.
        Loaded += (_, _) =>
        {
            if (_updates is not null) _updates.Changed += Show;
            Show();
        };
        Unloaded += (_, _) =>
        {
            if (_updates is not null) _updates.Changed -= Show;
        };
    }

    /// <summary>The updates the notice follows.</summary>
    public Updates? Updates
    {
        get => _updates;
        set
        {
            if (IsLoaded && _updates is not null) _updates.Changed -= Show;
            _updates = value;
            if (IsLoaded && _updates is not null) _updates.Changed += Show;
            Show();
        }
    }

    /// <summary>True: the note above the buttons (Simple mode); false: one row, the note first (Advanced mode).</summary>
    public bool Stacked
    {
        get => _stacked;
        set
        {
            _stacked = value;
            Arrange();
        }
    }

    /// <summary>
    /// What the notice shows for an update state, or null for nothing: its note, the download's progress in place of the
    /// button's label (null: the label), and whether ✕ is on offer. <paramref name="announce"/>: the version on offer
    /// wasn't skipped. A download goes on showing whichever way it was started.
    /// </summary>
    public static (string Note, string? Progress, bool CanSkip)? Look(UpdateState state, bool announce) => state.Stage switch
    {
        UpdateStage.Available when announce => (state.DownloadFailed ? "Couldn't download the update" : "A new version is available", null, true),
        UpdateStage.Downloading => ("A new version is available", $"{state.Percent}%", false),
        UpdateStage.Restarting => ("Restarting…", "100%", false),
        _ => null,
    };

    void Arrange()
    {
        Grid.SetRow(Buttons, _stacked ? 1 : 0);
        Grid.SetColumn(Buttons, _stacked ? 0 : 1);
        NoteLine.VerticalAlignment = _stacked ? VerticalAlignment.Top : VerticalAlignment.Center;
        NoteLine.Margin = _stacked ? new Thickness(0, 0, 0, StackedGap) : new Thickness(0, 0, RowGap, 0);
    }

    void Show()
    {
        var look = _updates is null ? null : Look(_updates.State, _updates.ShouldAnnounce);
        Visibility = look is null ? Visibility.Collapsed : Visibility.Visible;
        if (look is not { } shown) return;

        var (note, progress, canSkip) = shown;
        Note.Text = note;
        UpdateLabel.Visibility = progress is null ? Visibility.Visible : Visibility.Collapsed;
        Progress.Text = progress;
        Progress.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateButton.IsEnabled = progress is null;
        AutomationProperties.SetName(UpdateButton, progress is null ? "Update" : $"Downloading, {progress}");
        // Hidden rather than gone, so the button stays where it was.
        SkipButton.Visibility = canSkip ? Visibility.Visible : Visibility.Hidden;
    }

    void OnUpdateClick(object sender, RoutedEventArgs e) => App.Current.InstallUpdate();

    void OnSkipClick(object sender, RoutedEventArgs e) => _updates?.Skip();
}
