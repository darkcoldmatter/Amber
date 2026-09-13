using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Amber.Controls;

/// <summary>Time picker made of dropdowns: "9:00" + "PM", or a single "21:00", following the Windows clock format.</summary>
public partial class TimeSelector : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(TimeOnly), typeof(TimeSelector),
        new FrameworkPropertyMetadata(TimeOnly.MinValue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((TimeSelector)d).ShowTime()));

    readonly TimeSlots _slots = new(CultureInfo.CurrentCulture);
    bool _showingTime;

    public TimeSelector()
    {
        InitializeComponent();
        ClockBox.ItemsSource = _slots.ClockLabels;
        if (_slots.Is24Hour)
        {
            ClockBox.Width = 172;
            PeriodBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            PeriodBox.ItemsSource = _slots.PeriodLabels;
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
        (ClockBox.SelectedIndex, PeriodBox.SelectedIndex) = _slots.IndexesOf(Time);
        _showingTime = false;
    }

    void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showingTime) return;
        Time = _slots.TimeAt(ClockBox.SelectedIndex, PeriodBox.SelectedIndex);
    }
}
