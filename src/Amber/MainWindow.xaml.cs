using System.Windows;
using Amber.Tint;

namespace Amber;

public partial class MainWindow : Window
{
    readonly Settings _settings;

    public MainWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;

        var selected = settings.Mode switch
        {
            TintMode.Amber => AmberOption,
            TintMode.Astronomic => AstronomicOption,
            _ => GoldenHourOption,
        };
        selected.IsChecked = true;
    }

    void OnModeChecked(object sender, RoutedEventArgs e)
    {
        _settings.Mode = sender == AmberOption ? TintMode.Amber
            : sender == AstronomicOption ? TintMode.Astronomic
            : TintMode.GoldenHour;
    }
}
