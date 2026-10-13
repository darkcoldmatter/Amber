using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Amber.Views;

namespace Amber;

public partial class MainWindow : Window
{
    const double SimpleWidth = 724, AdvancedWidth = 848;

    readonly Settings _settings;

    public MainWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;
        ShowAppMode();

        settings.PropertyChanged += OnSettingChanged;
        Closed += (_, _) => settings.PropertyChanged -= OnSettingChanged;
        SizeChanged += OnSizeChanged;
    }

    void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.IsAdvanced)) ShowAppMode();
    }

    /// <summary>
    /// Simple and Advanced differ in width, body and header, and the ON/OFF button belongs to the mode you are in.
    /// </summary>
    void ShowAppMode()
    {
        var advanced = _settings.IsAdvanced;
        Root.Width = advanced ? AdvancedWidth : SimpleWidth;
        ScheduleButton.Visibility = advanced ? Visibility.Collapsed : Visibility.Visible;
        PowerButton.SetBinding(ToggleButton.IsCheckedProperty, advanced ? nameof(Settings.AdvancedIsOn) : nameof(Settings.IsOn));
        ModeHost.Content = advanced ? new AdvancedView(_settings) : new SimpleView(_settings);
        ShowGlow();
    }

    /// <summary>The glow is softer behind Advanced mode's timeline.</summary>
    void ShowGlow() => Glow.Opacity = _settings.IsAdvanced && OverlayHost.Content is null ? 0.3 : 0.7;

    /// <summary>
    /// When the window changes size it keeps its centre (Simple ↔ Advanced) and stays within the usable part of
    /// the screen, so growing near the bottom doesn't slide it under the taskbar.
    /// </summary>
    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.PreviousSize.Width == 0) return; // first layout: the window opens centred on screen
        var work = WorkArea();
        var left = Left - (e.NewSize.Width - e.PreviousSize.Width) / 2;
        Left = Math.Max(work.Left, Math.Min(left, work.Right - e.NewSize.Width));
        Top = Math.Max(work.Top, Math.Min(Top, work.Bottom - e.NewSize.Height));
    }

    /// <summary>The usable part (without the taskbar) of the monitor the window is on.</summary>
    Rect WorkArea()
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(this).Handle, MONITOR_DEFAULTTONEAREST), ref info);
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(info.rcWork.Left / dpi.DpiScaleX, info.rcWork.Top / dpi.DpiScaleY,
            (info.rcWork.Right - info.rcWork.Left) / dpi.DpiScaleX, (info.rcWork.Bottom - info.rcWork.Top) / dpi.DpiScaleY);
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

    /// <summary>Settings and About cover the whole window; closing them returns to the mode you came from.</summary>
    void ShowOverlay(UIElement view)
    {
        OverlayHost.Content = view;
        OverlayHost.Visibility = Visibility.Visible;
        ModeLayer.Visibility = Visibility.Collapsed;
        ShowGlow();
    }

    void CloseOverlay()
    {
        OverlayHost.Visibility = Visibility.Collapsed;
        OverlayHost.Content = null;
        ModeLayer.Visibility = Visibility.Visible;
        ShowGlow();
    }

    const int MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, int flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
}
