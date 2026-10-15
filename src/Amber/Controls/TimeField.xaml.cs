using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Amber.Controls;

/// <summary>
/// The "06 : 00" pill, "6 : 00 PM" on the 12-hour clock. Clicking the hours or the minutes opens a short list (the 24
/// hours, or 00/15/30/45); the mouse wheel over either one steps it, wrapping round. AM/PM works just as the hours do.
/// </summary>
public partial class TimeField : UserControl
{
    const double ListWidth = 56, TwelveHourListWidth = 72;

    readonly TimeFormat _format = TimeFormat.Current;
    int _wheel; // wheel movement not yet turned into a step

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(TimeOnly), typeof(TimeField),
        new FrameworkPropertyMetadata(default(TimeOnly), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (field, _) => ((TimeField)field).OnValueChanged()));

    public TimeField()
    {
        InitializeComponent();
        if (!_format.Is24Hour) ShowTwelveHourParts();
        ShowValue();
    }

    public TimeOnly Value { get => (TimeOnly)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Raised whenever the value changes, whether picked here or set from outside.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// "6 : 00 PM" takes 112 px, 25 more than "06 : 00": the hour and the colon keep their places and spacing, the
    /// minutes follow at the same distance, and AM/PM comes a short space after them. Each part's hover fills just it.
    /// </summary>
    void ShowTwelveHourParts()
    {
        HourColumn.Width = new GridLength(40);
        MinuteColumn.Width = new GridLength(34);
        DesignatorColumn.Width = new GridLength(38);
        Colon.HorizontalAlignment = HorizontalAlignment.Left;
        Colon.Margin = new Thickness(38, 0, 0, 0); // centred on the hours' right edge
        Minutes.Tag = new CornerRadius(0);
        Minutes.Padding = new Thickness(9, 0, 0, 0);
        Designator.Padding = new Thickness(0, 0, 10, 0);
        Designator.Visibility = Visibility.Visible;
    }

    void OnValueChanged()
    {
        ShowValue();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    void ShowValue()
    {
        Hours.Content = _format.FieldHour(Value.Hour);
        Minutes.Content = Value.Minute.ToString("00");
        Designator.Content = _format.Designator(Value.Hour);
    }

    /// <summary>The hours, or AM/PM: the list of hours opens under the part that was clicked.</summary>
    void OnHoursClick(object sender, RoutedEventArgs e) =>
        DropdownList.Open((Button)sender, _format.Hours, Value.Hour, _format.Is24Hour ? ListWidth : TwelveHourListWidth,
            hour => Value = new TimeOnly(hour, Value.Minute));

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
