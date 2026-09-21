using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Amber.Tint;

namespace Amber.Views;

public partial class FilterCard : UserControl
{
    readonly Filter _filter;
    readonly IReadOnlyList<Filter> _others;
    bool _filling;

    public FilterCard(Filter filter, IReadOnlyList<Filter> others)
    {
        InitializeComponent();
        _filter = filter;
        _others = others;
        DataContext = filter;

        StartHour.ItemsSource = TimeOptions.Hours;
        EndHour.ItemsSource = TimeOptions.Hours;
        StartMinute.ItemsSource = TimeOptions.Minutes;
        EndMinute.ItemsSource = TimeOptions.Minutes;
        foreach (var box in new[] { StartHour, StartMinute, EndHour, EndMinute }) box.SelectionChanged += OnTimeChanged;

        ModeName.Text = NameOf(filter.Mode);
        ModeIcon.Data = (Geometry)FindResource("LogoSun");
        ModeIcon.Fill = (Brush)FindResource(filter.Mode + "Accent");
        Level.Foreground = (Brush)FindResource(filter.Mode + "Accent");
        CurveToggle.Foreground = (Brush)FindResource(filter.Mode + "Label");

        // The curve editor arrives in the next step.
        CurveToggle.IsEnabled = false;
        CurveToggle.ToolTip = "Curve editing comes next";

        Level.PreviewStarted += (_, _) => App.Current.StartPreview(filter.Mode, filter.Level);
        Level.PreviewEnded += (_, _) => App.Current.EndPreview();
        Level.ValueChanged += (_, _) => App.Current.UpdatePreview(filter.Mode, filter.Level);

        ShowTimes();
        filter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Filter.Start) or nameof(Filter.End)) ShowTimes();
        };
    }

    public event EventHandler? DeleteRequested;

    void OnDelete(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this, EventArgs.Empty);

    void ShowTimes()
    {
        _filling = true;
        StartHour.SelectedIndex = _filter.Start.Hour;
        StartMinute.SelectedIndex = _filter.Start.Minute / TimeOptions.ScheduleStep;
        EndHour.SelectedIndex = _filter.End.Hour;
        EndMinute.SelectedIndex = _filter.End.Minute / TimeOptions.ScheduleStep;
        _filling = false;
    }

    /// <summary>Typing a time moves that edge, stopping at the neighbouring filters just like dragging does.</summary>
    void OnTimeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_filling) return;

        var movingStart = sender == StartHour || sender == StartMinute;
        var target = ScheduleRange.Minutes(movingStart
            ? new TimeOnly(Math.Max(StartHour.SelectedIndex, 0), Math.Max(StartMinute.SelectedIndex, 0) * TimeOptions.ScheduleStep)
            : new TimeOnly(Math.Max(EndHour.SelectedIndex, 0), Math.Max(EndMinute.SelectedIndex, 0) * TimeOptions.ScheduleStep));

        var others = _others.Where(f => f != _filter).Select(f => (ScheduleRange.Minutes(f.Start), ScheduleRange.Minutes(f.End)));
        var (start, end) = FilterLayout.MoveEdge((ScheduleRange.Minutes(_filter.Start), ScheduleRange.Minutes(_filter.End)), others, movingStart, target);
        _filter.Start = ScheduleRange.Time(start);
        _filter.End = ScheduleRange.Time(end);
        ShowTimes();   // snap the boxes back if the move was blocked
    }

    static string NameOf(TintMode mode) => mode switch
    {
        TintMode.GoldenHour => "Golden hour",
        TintMode.Amber => "Amber",
        _ => "Astronomic",
    };
}
