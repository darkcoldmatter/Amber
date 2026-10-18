using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Amber.Views;

public partial class SettingsView : UserControl
{
    readonly Updates _updates;

    public SettingsView(Updates updates)
    {
        InitializeComponent();
        _updates = updates;
        StartWithWindowsSwitch.IsChecked = StartupRegistration.IsEnabled;

        // Follow updates while Settings is open; let go when it closes, so a closed window isn't kept alive.
        Loaded += (_, _) =>
        {
            updates.Changed += ShowUpdateRow;
            ShowUpdateRow();
        };
        Unloaded += (_, _) => updates.Changed -= ShowUpdateRow;
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
        UpdateStage.Available => ($"Update to {state.Version}", true, state.DownloadFailed ? "Couldn't download" : null),
        UpdateStage.Downloading => ($"Downloading {state.Percent}%", false, null),
        UpdateStage.Restarting => ("Restarting…", false, null),
        UpdateStage.CheckFailed => ("Check", true, "Couldn't check"),
        UpdateStage.UpdateFailed => ("Check", true, "Couldn't update"),
        _ => ("Check", true, null),
    };

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
