using System.Windows;
using System.Windows.Controls;
using Amber.Views;

namespace Amber;

public partial class MainWindow : Window
{
    readonly Settings _settings;

    public MainWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        ShowAppMode();
    }

    void OnSwitchAppMode(object sender, RoutedEventArgs e)
    {
        _settings.IsAdvanced = !_settings.IsAdvanced;
        ShowAppMode();
    }

    /// <summary>The button always offers the mode you are not in.</summary>
    void ShowAppMode()
    {
        AppModeButton.Content = _settings.IsAdvanced ? "Simple" : "Advanced";
        ModeHost.Content = _settings.IsAdvanced ? AdvancedPlaceholder() : new SimpleView(_settings);
    }

    void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var view = new SettingsView();
        view.CloseRequested += (_, _) => CloseOverlay();
        ShowOverlay(view);
    }

    void OnOpenAbout(object sender, RoutedEventArgs e)
    {
        var view = new AboutView();
        view.CloseRequested += (_, _) => CloseOverlay();
        ShowOverlay(view);
    }

    void ShowOverlay(UIElement view)
    {
        OverlayHost.Content = view;
        OverlayHost.Visibility = Visibility.Visible;
        ModeLayer.Visibility = Visibility.Collapsed;
    }

    void CloseOverlay()
    {
        OverlayHost.Visibility = Visibility.Collapsed;
        OverlayHost.Content = null;
        ModeLayer.Visibility = Visibility.Visible;
    }

    // Advanced mode is built in the next step.
    static UIElement AdvancedPlaceholder() => new TextBlock
    {
        Text = "Advanced mode is coming in the next step.",
        Margin = new Thickness(30, 40, 30, 60),
        FontSize = 14,
    };
}
