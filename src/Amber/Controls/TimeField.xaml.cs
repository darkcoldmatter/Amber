using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Amber.Controls;

/// <summary>
/// The "06 : 00" pill. Clicking the hours or the minutes opens a short list (00–23, or 00/15/30/45);
/// the mouse wheel over either one steps it, wrapping round.
/// </summary>
public partial class TimeField : UserControl
{
    const double ListWidth = 56;

    int _wheel; // wheel movement not yet turned into a step

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(TimeOnly), typeof(TimeField),
        new FrameworkPropertyMetadata(default(TimeOnly), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (field, _) => ((TimeField)field).OnValueChanged()));

    public TimeField()
    {
        InitializeComponent();
        ShowValue();
    }

    public TimeOnly Value { get => (TimeOnly)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Raised whenever the value changes, whether picked here or set from outside.</summary>
    public event EventHandler? ValueChanged;

    void OnValueChanged()
    {
        ShowValue();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    void ShowValue()
    {
        Hours.Content = Value.Hour.ToString("00");
        Minutes.Content = Value.Minute.ToString("00");
    }

    void OnHoursClick(object sender, RoutedEventArgs e) =>
        DropdownList.Open(Hours, TimeOptions.Hours, Value.Hour, ListWidth, hour => Value = new TimeOnly(hour, Value.Minute));

    void OnMinutesClick(object sender, RoutedEventArgs e) =>
        DropdownList.Open(Minutes, TimeOptions.Minutes, Value.Minute / TimeOptions.ScheduleStep, ListWidth,
            step => Value = new TimeOnly(Value.Hour, step * TimeOptions.ScheduleStep));

    void OnHoursWheel(object sender, MouseWheelEventArgs e)
    {
        var hour = Value.Hour + WheelSteps(e);
        Value = new TimeOnly((hour % 24 + 24) % 24, Value.Minute);
        e.Handled = true;
    }

    void OnMinutesWheel(object sender, MouseWheelEventArgs e)
    {
        var count = 60 / TimeOptions.ScheduleStep;
        var step = Value.Minute / TimeOptions.ScheduleStep + WheelSteps(e);
        Value = new TimeOnly(Value.Hour, (step % count + count) % count * TimeOptions.ScheduleStep);
        e.Handled = true;
    }

    /// <summary>
    /// Whole wheel notches turned so far. Precision touchpads send many small deltas per notch,
    /// so the rest is kept for the next event (until the pointer leaves the part).
    /// </summary>
    int WheelSteps(MouseWheelEventArgs e)
    {
        _wheel += e.Delta;
        var steps = _wheel / Mouse.MouseWheelDeltaForOneLine;
        _wheel -= steps * Mouse.MouseWheelDeltaForOneLine;
        return steps;
    }

    void OnPartMouseLeave(object sender, MouseEventArgs e) => _wheel = 0;
}
