using System.Windows;
using Amber.Tint;

namespace Amber;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    void OnLoaded(object sender, RoutedEventArgs e) => UpdateTint();
    void OnSettingChanged(object sender, RoutedEventArgs e) => UpdateTint();
    void OnLevelChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateTint();

    void UpdateTint()
    {
        if (!IsLoaded) return; // Controls raise change events while the window is still being built.

        LevelText.Text = Math.Round(LevelSlider.Value).ToString();

        if (OnSwitch.IsChecked != true)
        {
            ScreenTint.Reset();
            return;
        }

        var mode = AmberOption.IsChecked == true ? TintMode.Amber
            : AstronomicOption.IsChecked == true ? TintMode.Astronomic
            : TintMode.GoldenHour;
        ScreenTint.Apply(TintMatrix.Build(mode, LevelSlider.Value));
    }
}
