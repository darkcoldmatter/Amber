using System.Windows;
using System.Windows.Controls;

namespace Amber.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        StartWithWindowsSwitch.IsChecked = StartupRegistration.IsEnabled;
    }

    public event EventHandler? CloseRequested;

    void OnStartWithWindowsClick(object sender, RoutedEventArgs e) =>
        StartupRegistration.IsEnabled = StartWithWindowsSwitch.IsChecked == true;

    void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
