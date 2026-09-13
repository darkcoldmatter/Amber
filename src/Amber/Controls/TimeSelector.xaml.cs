using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Amber.Controls;

/// <summary>Hour and minute dropdowns, plus AM/PM when Windows uses a 12-hour clock.</summary>
public partial class TimeSelector : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(TimeOnly), typeof(TimeSelector),
        new FrameworkPropertyMetadata(TimeOnly.MinValue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((TimeSelector)d).ShowTime()));

    readonly ClockFormat _clock = new(CultureInfo.CurrentCulture);
    bool _showingTime;

    public TimeSelector()
    {
        InitializeComponent();
        HourBox.ItemsSource = _clock.HourLabels;
        MinuteBox.ItemsSource = _clock.MinuteLabels;

        // Both layouts fill the same 172 px as the Figma design.
        if (_clock.Is24Hour)
        {
            HourBox.Width = MinuteBox.Width = 84;
            PeriodBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            HourBox.Width = MinuteBox.Width = 52;
            PeriodBox.Width = 60;
            PeriodBox.ItemsSource = _clock.PeriodLabels;
        }
        ShowTime();
    }

    public TimeOnly Time
    {
        get => (TimeOnly)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    void ShowTime()
    {
        _showingTime = true;
        (HourBox.SelectedIndex, MinuteBox.SelectedIndex, PeriodBox.SelectedIndex) = _clock.IndexesOf(Time);
        _showingTime = false;
    }

    void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showingTime) return;
        Time = _clock.TimeAt(HourBox.SelectedIndex, MinuteBox.SelectedIndex, PeriodBox.SelectedIndex);
    }
}
