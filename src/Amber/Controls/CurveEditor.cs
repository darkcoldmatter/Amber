using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// The level curve of one filter. Left click adds a point, right click removes it, and dragging moves it —
/// the ends only move up and down. A dark box to the left of the point shows the time and level while dragging.
/// </summary>
public class CurveEditor : FrameworkElement
{
    const double PlotLeft = 28, PlotTop = 24, PlotWidth = 554, PlotHeight = 140, ValueInset = 4.5;
    const double PointRadius = 5, GrabRadius = 9, MinLabelGap = 28;

    static readonly Brush GridBrush = Frozen("#EFF1F5");
    static readonly Brush EdgeBrush = Frozen("#E6E6E2");
    static readonly Brush LabelBrush = Frozen("#565656");
    static readonly Brush BoxBrush = Frozen("#271F1F");
    static readonly Brush BoxSubBrush = Frozen("#A5AABA");

    Filter? _filter;
    int _dragging = -1;

    public Filter? Filter
    {
        get => _filter;
        set
        {
            if (_filter is not null) _filter.PropertyChanged -= OnFilterChanged;
            _filter = value;
            if (_filter is not null) _filter.PropertyChanged += OnFilterChanged;
            InvalidateVisual();
        }
    }

    public Brush Accent { get; set; } = Brushes.Orange;

    void OnFilterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_filter is null) return;

        var point = e.GetPosition(this);
        var index = PointAt(point);
        if (index < 0)
        {
            if (!InPlot(point)) return;
            _filter.Curve = CurveMath.Add(_filter.Curve, MinutesAt(point.X), LevelAt(point.Y), _filter.LengthMinutes);
            index = PointAt(point);
            if (index < 0) return;
        }

        _dragging = index;
        CaptureMouse();
        App.Current.StartPreview(_filter.Mode, _filter.Curve[index].Level);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_filter is null) return;

        if (_dragging < 0)
        {
            Cursor = PointAt(e.GetPosition(this)) >= 0 ? Cursors.SizeAll : Cursors.Arrow;
            return;
        }

        var point = e.GetPosition(this);
        _filter.Curve = CurveMath.Move(_filter.Curve, _dragging, MinutesAt(point.X), LevelAt(point.Y), _filter.LengthMinutes);
        App.Current.UpdatePreview(_filter.Mode, _filter.Curve[_dragging].Level);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging < 0) return;

        _dragging = -1;
        ReleaseMouseCapture();
        App.Current.EndPreview();
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (_filter is null) return;

        var index = PointAt(e.GetPosition(this));
        if (index < 0) return;

        _filter.Curve = CurveMath.Remove(_filter.Curve, index);
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_filter is null) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var family = TextElement.GetFontFamily(this);
        var plot = new Rect(PlotLeft, PlotTop, PlotWidth, PlotHeight);

        dc.DrawRoundedRectangle(Brushes.White, new Pen(EdgeBrush, 1), plot, 8, 8);

        foreach (var level in new[] { 0, 25, 50, 75, 100 })
        {
            var y = Y(level);
            dc.DrawLine(new Pen(GridBrush, 1), new Point(PlotLeft, y), new Point(PlotLeft + PlotWidth, y));
            var label = Text(level.ToString(CultureInfo.InvariantCulture), family, 12, FontWeights.Medium, LabelBrush, dpi);
            dc.DrawText(label, new Point(0, y - label.Height / 2));
        }

        DrawHours(dc, family, dpi);

        var curve = _filter.Curve;
        if (curve.Count == 0) return;

        var line = new PathFigure { StartPoint = new Point(X(curve[0].Minutes), Y(curve[0].Level)) };
        foreach (var point in curve.Skip(1)) line.Segments.Add(new LineSegment(new Point(X(point.Minutes), Y(point.Level)), true));

        var area = line.Clone();
        area.Segments.Add(new LineSegment(new Point(X(curve[^1].Minutes), plot.Bottom), true));
        area.Segments.Add(new LineSegment(new Point(X(curve[0].Minutes), plot.Bottom), true));
        area.IsClosed = true;

        var fill = Accent.Clone();
        fill.Opacity = 0.4;
        dc.DrawGeometry(fill, null, new PathGeometry([area]));
        dc.DrawGeometry(null, new Pen(Accent, 1.5), new PathGeometry([line]));

        foreach (var point in curve)
            dc.DrawEllipse(Accent, new Pen(Brushes.White, 1.5), new Point(X(point.Minutes), Y(point.Level)), PointRadius, PointRadius);

        if (_dragging >= 0 && _dragging < curve.Count) DrawValueBox(dc, family, dpi, curve[_dragging]);
    }

    /// <summary>Hour marks across the range; labels thin out when they would collide, the lines stay.</summary>
    void DrawHours(DrawingContext dc, FontFamily family, double dpi)
    {
        var length = _filter!.LengthMinutes;
        var startMinutes = ScheduleRange.Minutes(_filter.Start);
        var marks = new List<(double X, string Label)>();

        for (var minute = 0; minute <= length; minute++)
        {
            if ((startMinutes + minute) % 60 != 0) continue;
            var hour = (startMinutes + minute) / 60 % 24;
            marks.Add((X(minute), hour.ToString("00", CultureInfo.InvariantCulture)));
        }

        // A range shorter than an hour gets its own two times instead.
        if (marks.Count < 2)
        {
            marks.Clear();
            marks.Add((X(0), _filter.Start.ToString("HH:mm")));
            marks.Add((X(length), _filter.End.ToString("HH:mm")));
        }

        var everyOther = marks.Count > 1 && marks[1].X - marks[0].X < MinLabelGap;
        for (var i = 0; i < marks.Count; i++)
        {
            var (x, label) = marks[i];
            dc.DrawLine(new Pen(GridBrush, 1), new Point(x, PlotTop), new Point(x, PlotTop + PlotHeight));
            if (everyOther && i % 2 == 1 && i != marks.Count - 1) continue;

            var text = Text(label, family, 14, FontWeights.Bold, LabelBrush, dpi);
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 0, ActualWidth - text.Width), 8 - text.Height / 2));
        }
    }

    /// <summary>The dark readout sits to the left of the point, and stays inside the control.</summary>
    void DrawValueBox(DrawingContext dc, FontFamily family, double dpi, CurvePoint point)
    {
        var time = _filter!.Start.Add(TimeSpan.FromMinutes(point.Minutes)).ToString("HH:mm");
        var timeText = Text(time, family, 13, FontWeights.Bold, Brushes.White, dpi);
        var levelText = Text($"{Math.Round(point.Level)}%", family, 12, FontWeights.Medium, BoxSubBrush, dpi);

        var width = Math.Max(timeText.Width, levelText.Width) + 20;
        var height = timeText.Height + levelText.Height + 14;
        var left = Math.Max(X(point.Minutes) - width - 12, 0);
        var top = Math.Clamp(Y(point.Level) - height / 2, 0, ActualHeight - height);

        dc.DrawRoundedRectangle(BoxBrush, null, new Rect(left, top, width, height), 8, 8);
        dc.DrawText(timeText, new Point(left + 10, top + 7));
        dc.DrawText(levelText, new Point(left + 10, top + 7 + timeText.Height));
    }

    int PointAt(Point position)
    {
        if (_filter is null) return -1;
        for (var i = 0; i < _filter.Curve.Count; i++)
        {
            var point = _filter.Curve[i];
            if (Math.Abs(X(point.Minutes) - position.X) <= GrabRadius && Math.Abs(Y(point.Level) - position.Y) <= GrabRadius) return i;
        }
        return -1;
    }

    static bool InPlot(Point position) =>
        position.X >= PlotLeft && position.X <= PlotLeft + PlotWidth && position.Y >= PlotTop && position.Y <= PlotTop + PlotHeight;

    double X(int minutes) => PlotLeft + minutes / (double)Math.Max(_filter!.LengthMinutes, 1) * PlotWidth;
    static double Y(double level) => PlotTop + ValueInset + (1 - Math.Clamp(level, 0, 100) / 100) * (PlotHeight - 2 * ValueInset);

    double MinutesAt(double x) => (x - PlotLeft) / PlotWidth * _filter!.LengthMinutes;
    static double LevelAt(double y) => (1 - (y - PlotTop - ValueInset) / (PlotHeight - 2 * ValueInset)) * 100;

    static FormattedText Text(string text, FontFamily family, double size, FontWeight weight, Brush brush, double dpi) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), size, brush, dpi);

    static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
