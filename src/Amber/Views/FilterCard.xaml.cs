using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Amber.Tint;

namespace Amber.Views;

public partial class FilterCard : UserControl
{
    const double CollapsedHeight = 130, ExpandedHeight = 319;

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

        Curve.Accent = (Brush)FindResource(filter.Mode + "Accent");
        ShowCurve(filter.CurveEnabled, animate: false);
        CurveToggle.Checked += (_, _) => ShowCurve(true, animate: true);
        CurveToggle.Unchecked += (_, _) => ShowCurve(false, animate: true);

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

    /// <summary>
    /// The curve replaces the level slider: the card grows and the graph fades in. Collapsing keeps the
    /// curve for next time and goes back to the flat level.
    /// </summary>
    void ShowCurve(bool show, bool animate)
    {
        if (show && _filter.Curve.Count == 0) _filter.ResetCurve();
        Curve.Filter = show ? _filter : null;

        var duration = new Duration(TimeSpan.FromSeconds(animate ? 0.25 : 0));
        Card.BeginAnimation(HeightProperty, new DoubleAnimation(show ? ExpandedHeight : CollapsedHeight, duration));

        Fade(Curve, show ? 1 : 0, animate);
        Fade(CurveHint, show ? 1 : 0, animate);
        Fade(Level, show ? 0 : 1, animate);
        Fade(LevelValue, show ? 0 : 1, animate);
        Level.IsHitTestVisible = !show;
    }

    static void Fade(UIElement element, double to, bool animate) =>
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(to, new Duration(TimeSpan.FromSeconds(animate ? 0.3 : 0))));

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
