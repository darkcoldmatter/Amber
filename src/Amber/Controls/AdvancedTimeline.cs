using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Amber.Tint;

namespace Amber.Controls;

/// <summary>
/// The 24-hour timeline of advanced mode: one bar per filter, each showing its level as a white line.
/// Edges drag like the simple bar, but stop at the neighbouring filters.
/// </summary>
public class AdvancedTimeline : FrameworkElement
{
    const double BarTop = 28, BarHeight = 60, CornerRadius = 6, GrabWidth = 8, LineInset = 4, SideGap = 1;
    const double LabelCenterY = 4.5, TickBottom = 22, LongTick = 9, ShortTick = 6;

    static readonly Brush TrackBrush = Frozen("#F5F3EE");
    static readonly Brush TickBrush = Frozen("#D3D6DF");
    static readonly Brush LabelBrush = Frozen("#565656");
    static readonly Dictionary<TintMode, Brush> Accents = new()
    {
        [TintMode.GoldenHour] = Frozen("#FEC960"),
        [TintMode.Amber] = Frozen("#F99E43"),
        [TintMode.DeepNight] = Frozen("#FD867F"),
    };

    IReadOnlyList<Filter> _filters = [];
    Filter? _dragging;
    bool _draggingStart;

    public IReadOnlyList<Filter> Filters
    {
        get => _filters;
        set
        {
            foreach (var filter in _filters) filter.PropertyChanged -= OnFilterChanged;
            _filters = value;
            foreach (var filter in _filters) filter.PropertyChanged += OnFilterChanged;
            InvalidateVisual();
        }
    }

    void OnFilterChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.GetPosition(this).X;

        if (_dragging is not null)
        {
            var others = _filters.Where(f => f != _dragging).Select(Range).ToArray();
            var (start, end) = FilterLayout.MoveEdge(Range(_dragging), others, _draggingStart, ScheduleRange.MinutesAt(x, ActualWidth));
            _dragging.Start = ScheduleRange.Time(start);
            _dragging.End = ScheduleRange.Time(end);
            return;
        }

        Cursor = EdgeAt(x) is null ? Cursors.Arrow : Cursors.SizeWE;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (EdgeAt(e.GetPosition(this).X) is not var (filter, isStart)) return;

        _dragging = filter;
        _draggingStart = isStart;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragging = null;
        ReleaseMouseCapture();
    }

    (Filter Filter, bool IsStart)? EdgeAt(double x)
    {
        foreach (var filter in _filters)
        {
            if (Math.Abs(x - X(ScheduleRange.Minutes(filter.Start))) <= GrabWidth) return (filter, true);
            if (Math.Abs(x - X(ScheduleRange.Minutes(filter.End))) <= GrabWidth) return (filter, false);
        }
        return null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

        for (var hour = 0; hour <= 24; hour++)
        {
            var x = Math.Round(hour * ActualWidth / 24) + 0.5;
            var isLabelled = hour % 2 == 0;
            dc.DrawLine(new Pen(TickBrush, 1), new Point(x, TickBottom - (isLabelled ? LongTick : ShortTick)), new Point(x, TickBottom));
            if (!isLabelled) continue;

            var text = new FormattedText(hour.ToString("00", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, LabelBrush, dpi);
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 0, ActualWidth - text.Width), LabelCenterY - text.Height / 2));
        }

        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, BarTop, ActualWidth, BarHeight), CornerRadius, CornerRadius);

        foreach (var filter in _filters)
        {
            var start = ScheduleRange.Minutes(filter.Start);
            var length = filter.LengthMinutes;
            var firstPiece = Math.Min(length, ScheduleRange.DayMinutes - start);

            DrawPiece(dc, filter, start, firstPiece, 0);
            if (firstPiece < length) DrawPiece(dc, filter, 0, length - firstPiece, firstPiece);
        }
    }

    /// <summary>Draws one visible piece of a filter; a filter that runs past midnight has two.</summary>
    void DrawPiece(DrawingContext dc, Filter filter, int startMinute, int minutes, int minutesAlready)
    {
        var left = X(startMinute) + SideGap;
        var right = X(startMinute + minutes) - SideGap;
        if (right <= left) return;

        dc.DrawRoundedRectangle(Accents[filter.Mode], null, new Rect(left, BarTop, right - left, BarHeight), CornerRadius, CornerRadius);

        var line = new StreamGeometry();
        using (var draw = line.Open())
        {
            var step = Math.Max(5, minutes / 60);
            var started = false;
            for (var offset = 0; offset <= minutes; offset += step)
            {
                var point = new Point(X(startMinute + offset), LevelY(filter.LevelAt(minutesAlready + offset)));
                if (started) draw.LineTo(point, true, false);
                else { draw.BeginFigure(point, false, false); started = true; }
            }
        }
        line.Freeze();
        dc.PushClip(new RectangleGeometry(new Rect(left, BarTop, right - left, BarHeight)));
        dc.DrawGeometry(null, new Pen(Brushes.White, 1.5), line);
        dc.Pop();
    }

    /// <summary>0 and 100 stay inside the bar, so the line is visible at both extremes.</summary>
    static double LevelY(double level) => BarTop + LineInset + (1 - Math.Clamp(level, 0, 100) / 100) * (BarHeight - 2 * LineInset);

    double X(int minutes) => minutes / (double)ScheduleRange.DayMinutes * ActualWidth;

    static (int Start, int End) Range(Filter filter) => (ScheduleRange.Minutes(filter.Start), ScheduleRange.Minutes(filter.End));

    static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
