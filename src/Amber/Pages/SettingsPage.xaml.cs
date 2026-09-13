using System.Windows;
using System.Windows.Controls;

namespace Amber.Pages;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        StartWithWindowsSwitch.IsChecked = StartupRegistration.IsEnabled;
    }

    void OnStartWithWindowsClick(object sender, RoutedEventArgs e)
    {
        StartupRegistration.IsEnabled = StartWithWindowsSwitch.IsChecked == true;
    }
}
