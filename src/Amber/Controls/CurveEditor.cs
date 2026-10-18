using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>A time over the curve: minutes from the start of the range, its text, and whether it is a half hour (drawn faded).</summary>
public readonly record struct TimeLabel(int Minutes, string Text, bool Faded);

/// <summary>
/// The level curve of one filter, drawn as Figma's graph card (111:2717): times along the top, the plot with its grid,
/// the curve over a fill in the bar colour, and a hint underneath. Left click adds a point, right click removes one, and
/// dragging moves it — the ends only up and down. While dragging, a dark box beside the point shows its time and level.
/// </summary>
public class CurveEditor : FrameworkElement
{
    // In the card: the plot's outer edge (24 px in from the sides, its 1 px border inside), the label and hint lines.
    const double PlotLeft = 24, PlotTop = 44, PlotHeight = 140, PlotRadius = 6, LabelsMiddle = 28, HintMiddle = 212.5;
    // Points keep 5 px off the plot's inner edges, so the ends and 0 / 100 % show whole.
    const double Inset = 5, PointRadius = 5, GrabRadius = 9;
    const double BoxGap = 14, BoxPadding = 8, BoxHeight = 40, BoxRadius = 8;
    // Room between time labels; a label closer than EndGap to an end label would read as one with it.
    const double MinLabelGap = 16, EndGap = 4;
    const double TextSize = 12; // every text on the card
    const string Hint = "Click to add a point. Right-click a point to remove it.";

    static readonly int[] LabelStrides = [30, 60, 120, 180, 240, 360, 720];
    static readonly int[] LevelLines = [25, 50, 75];

    static readonly Brush InkBrush = Timeline.Frozen(new SolidColorBrush(Color.FromRgb(0x27, 0x1F, 0x1F)));
    static readonly Brush FadedInkBrush = Timeline.Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0x27, 0x1F, 0x1F)));
    static readonly Brush FadedWhiteBrush = Timeline.Frozen(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)));
    static readonly Brush HintBrush = Timeline.Frozen(new SolidColorBrush(Color.FromRgb(0xA5, 0xAA, 0xBA)));
    static readonly Pen BorderPen = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0xD3, 0xD6, 0xDF)), 1));
    // Figma's 0.5 px lines, as 1 px at half strength; the half-hour lines are 1 px, 3 px dashes.
    static readonly Pen ThinLine = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0xD3, 0xD6, 0xDF)), 1));
    static readonly Pen DashedLine = Timeline.Frozen(new Pen(BorderPen.Brush, 1) { DashStyle = new DashStyle([3, 3], 0), DashCap = PenLineCap.Flat });
    static readonly Pen CurvePen = Timeline.Frozen(new Pen(InkBrush, 1.5) { LineJoin = PenLineJoin.Round });

    Filter? _filter;
    int _dragging = -1;

    /// <summary>The filter whose curve is shown and edited, or null for none.</summary>
    public Filter? Filter
    {
        get => _filter;
        set
        {
            // The filter can go mid-drag (Esc, Delete): letting go of the mouse ends the drag and its preview first.
            if (_dragging >= 0) ReleaseMouseCapture();
            _dragging = -1;
            if (_filter is not null) _filter.PropertyChanged -= OnFilterChanged;
            _filter = value;
            if (_filter is not null) _filter.PropertyChanged += OnFilterChanged;
            InvalidateVisual();
        }
    }

    void OnFilterChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    /// <summary>
    /// The times along the top. The range's own start and end always show, pinned to the ends of the plot (a range under
    /// an hour shows only those). Between them, a regular stride on the clock: every half hour when it fits, else every
    /// 1, 2, 3, 4, 6 or 12 hours counted from the first whole hour — every label, an end on the stride included, keeps
    /// 16 px of room. A stride label gives way only to an end that falls between strides and that it would run into.
    /// On the 12-hour clock the two ends say AM or PM, as does 12 o'clock. <paramref name="measure"/> gives a label's width.
    /// </summary>
    public static List<TimeLabel> TimeLabels(int start, int length, double width, TimeFormat format, Func<string, double> measure)
    {
        TimeLabel At(int minutes) =>
            new(minutes, format.GraphLabel(start + minutes, designator: minutes == 0 || minutes == length), (start + minutes) % 60 == 30);
        (double Left, double Right) Box(TimeLabel label)
        {
            var labelWidth = measure(label.Text);
            var left = Math.Clamp(label.Minutes / (double)length * width - labelWidth / 2, 0, width - labelWidth);
            return (left, left + labelWidth);
        }

        var (first, last) = (At(0), At(length));
        if (length < 60) return [first, last];
        var (firstBox, lastBox) = (Box(first), Box(last));

        foreach (var stride in LabelStrides)
        {
            var origin = stride == 30 ? 30 : 60;
            List<TimeLabel> marks = [];
            for (var minutes = (origin - start % origin) % origin; minutes <= length; minutes += stride) marks.Add(At(minutes));
            var boxes = marks.Select(Box).ToArray();
            if (boxes.Zip(boxes.Skip(1)).Any(pair => pair.Second.Left - pair.First.Right < MinLabelGap)) continue;

            var between = marks.Where((mark, i) => mark.Minutes > 0 && mark.Minutes < length
                && boxes[i].Left >= firstBox.Right + EndGap && boxes[i].Right + EndGap <= lastBox.Left);
            return [first, .. between, last];
        }
        return [first, last];
    }

    /// <summary>Where the dark box goes: to the left of the point, or to its right when there's no room; never outside the bounds.</summary>
    public static Rect ValueBoxAt(Point point, Size box, Rect bounds)
    {
        var left = point.X - BoxGap - box.Width;
        if (left < bounds.Left) left = Math.Min(point.X + BoxGap, bounds.Right - box.Width);
        var top = Math.Clamp(point.Y - box.Height / 2, bounds.Top, bounds.Bottom - box.Height);
        return new Rect(left, top, box.Width, box.Height);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_filter is null) return;

        var point = e.GetPosition(this);
        var index = PointAt(point);
        if (index < 0)
        {
            if (!Inner.Contains(point)) return;
            _filter.Curve = CurveMath.Add(_filter.Curve, MinutesAt(point.X), LevelAt(point.Y), _filter.LengthMinutes);
            index = PointAt(point);
            if (index < 0) return;
        }
        if (!CaptureMouse()) return; // without the mouse, the drag (and its preview) could never end

        _dragging = index;
        App.Current.StartPreview(_filter.Mode, _filter.Curve[index].Level);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_filter is null) return;

        var point = e.GetPosition(this);
        if (_dragging < 0)
        {
            Cursor = PointAt(point) >= 0 ? Cursors.SizeAll : Cursors.Arrow;
            return;
        }

        _filter.Curve = CurveMath.Move(_filter.Curve, _dragging, MinutesAt(point.X), LevelAt(point.Y), _filter.LengthMinutes);
        App.Current.UpdatePreview(_filter.Mode, _filter.Curve[_dragging].Level);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging >= 0) ReleaseMouseCapture();
    }

    /// <summary>Losing the mouse mid-drag (Alt+Tab, a window popping up) must end the preview too, or the tint would stay.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragging < 0) return;
        _dragging = -1;
        App.Current.EndPreview();
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (_filter is null || _dragging >= 0) return;

        var index = PointAt(e.GetPosition(this));
        if (index < 0) return;

        _filter.Curve = CurveMath.Remove(_filter.Curve, index);
        e.Handled = true;
    }

    Rect Plot => new(PlotLeft, PlotTop, Math.Max(ActualWidth - 2 * PlotLeft, 2), PlotHeight);

    /// <summary>Inside the plot's border: where the grid, the fill and the curve are drawn.</summary>
    Rect Inner => new(PlotLeft + 1, PlotTop + 1, Plot.Width - 2, PlotHeight - 2);

    int Length => Math.Max(_filter!.LengthMinutes, 1);

    double X(int minutes) => Inner.Left + minutes / (double)Length * Inner.Width;

    /// <summary>Where a point is drawn: on its time, but kept off the plot's edges so the two ends show whole.</summary>
    double PointX(int minutes) => Math.Clamp(X(minutes), Inner.Left + Inset, Inner.Right - Inset);

    double Y(double level) => Inner.Top + Inset + (1 - Math.Clamp(level, 0, 100) / 100) * (Inner.Height - 2 * Inset);

    double MinutesAt(double x) => (x - Inner.Left) / Inner.Width * Length;
    double LevelAt(double y) => (1 - (y - Inner.Top - Inset) / (Inner.Height - 2 * Inset)) * 100;

    int PointAt(Point position)
    {
        if (_filter is null) return -1;
        for (var i = 0; i < _filter.Curve.Count; i++)
        {
            var point = _filter.Curve[i];
            if (Math.Abs(PointX(point.Minutes) - position.X) <= GrabRadius && Math.Abs(Y(point.Level) - position.Y) <= GrabRadius) return i;
        }
        return -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Clicks only reach what is painted: cover the whole card, so the empty plot above the curve takes them too.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        if (_filter is null) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var numbers = (FontFamily)FindResource("NumberFont");
        DrawLabels(dc, numbers, dpi);

        var inner = Inner;
        dc.PushClip(new RectangleGeometry(inner, PlotRadius - 1, PlotRadius - 1));
        DrawGrid(dc, inner);
        var curve = _filter.Curve;
        if (curve.Count > 0) DrawCurve(dc, inner, curve);
        dc.Pop();

        var border = Plot;
        border.Inflate(-0.5, -0.5);
        dc.DrawRoundedRectangle(null, BorderPen, border, PlotRadius - 0.5, PlotRadius - 0.5);

        foreach (var point in curve) dc.DrawEllipse(InkBrush, null, new Point(PointX(point.Minutes), Y(point.Level)), PointRadius, PointRadius);
        if (_dragging >= 0 && _dragging < curve.Count) DrawValueBox(dc, curve[_dragging], numbers, dpi);

        var words = new Typeface((FontFamily)FindResource("AppFont"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        var hint = Text(Hint, words, HintBrush, dpi);
        dc.DrawText(hint, new Point((ActualWidth - hint.Width) / 2, CapTop(words, HintMiddle, hint)));
    }

    /// <summary>Full hours in ink, half hours faded; the first and last pinned to the plot's ends, the rest centred on their time.</summary>
    void DrawLabels(DrawingContext dc, FontFamily font, double dpi)
    {
        var typeface = new Typeface(font, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        var plot = Plot;
        var start = ScheduleRange.Minutes(_filter!.Start);
        foreach (var label in TimeLabels(start, _filter.LengthMinutes, plot.Width, TimeFormat.Current, text => Text(text, typeface, InkBrush, dpi).Width))
        {
            var text = Text(label.Text, typeface, label.Faded ? FadedInkBrush : InkBrush, dpi);
            var x = Math.Clamp(plot.Left + label.Minutes / (double)Length * plot.Width - text.Width / 2, plot.Left, plot.Right - text.Width);
            dc.DrawText(text, new Point(x, CapTop(typeface, LabelsMiddle, text)));
        }
    }

    /// <summary>Lines at 25, 50 and 75 %, and at every half hour: solid on the hour, dashed in between. On whole pixels, so they stay sharp.</summary>
    void DrawGrid(DrawingContext dc, Rect inner)
    {
        foreach (var level in LevelLines)
        {
            var y = Math.Floor(Y(level)) + 0.5;
            dc.DrawLine(ThinLine, new Point(inner.Left, y), new Point(inner.Right, y));
        }

        var start = ScheduleRange.Minutes(_filter!.Start);
        for (var minutes = (30 - start % 30) % 30; minutes < Length; minutes += 30)
        {
            if (minutes == 0) continue;
            var x = Math.Floor(X(minutes)) + 0.5;
            dc.DrawLine((start + minutes) % 60 == 0 ? ThinLine : DashedLine, new Point(x, inner.Top), new Point(x, inner.Bottom));
        }
    }

    /// <summary>
    /// The curve through its points, carried on flat to the plot's edges, over a fill in the bar colour that fades out
    /// from the curve's highest point down to the bottom of the plot.
    /// </summary>
    void DrawCurve(DrawingContext dc, Rect inner, List<CurvePoint> curve)
    {
        var points = curve.Select(point => new Point(PointX(point.Minutes), Y(point.Level))).ToList();
        points.Insert(0, new Point(inner.Left, points[0].Y));
        points.Add(new Point(inner.Right, points[^1].Y));

        var area = new StreamGeometry();
        using (var draw = area.Open())
        {
            draw.BeginFigure(new Point(inner.Left, inner.Bottom), true, true);
            draw.PolyLineTo(points, false, true);
            draw.LineTo(new Point(inner.Right, inner.Bottom), false, true);
        }
        area.Freeze();

        var color = ((SolidColorBrush)FindResource(_filter!.Mode + "Bar")).Color;
        var fill = new LinearGradientBrush(Color.FromArgb(0x80, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B), 90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);

        var line = new StreamGeometry();
        using (var draw = line.Open())
        {
            draw.BeginFigure(points[0], false, false);
            draw.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        line.Freeze();
        dc.DrawGeometry(null, CurvePen, line);
    }

    /// <summary>The dark box beside the dragged point: its time over its level.</summary>
    void DrawValueBox(DrawingContext dc, CurvePoint point, FontFamily font, double dpi)
    {
        var bold = new Typeface(font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var medium = new Typeface(font, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        var time = Text(TimeFormat.Current.Clock(ScheduleRange.Minutes(_filter!.Start) + point.Minutes), bold, Brushes.White, dpi);
        var level = Text(point.Level.ToString("0", CultureInfo.InvariantCulture) + "%", medium, FadedWhiteBrush, dpi);

        var size = new Size(Math.Ceiling(Math.Max(time.Width, level.Width)) + 2 * BoxPadding, BoxHeight);
        var box = ValueBoxAt(new Point(PointX(point.Minutes), Y(point.Level)), size, new Rect(RenderSize));
        box = new Rect(Math.Round(box.X), Math.Round(box.Y), box.Width, box.Height);

        dc.DrawRoundedRectangle(InkBrush, null, box, BoxRadius, BoxRadius);
        dc.DrawText(time, new Point(box.Left + BoxPadding, CapTop(bold, box.Top + 12.5, time)));
        dc.DrawText(level, new Point(box.Left + BoxPadding, CapTop(medium, box.Top + 27.5, level)));
    }

    /// <summary>Where to draw a line of text so its capitals are centred on <paramref name="middle"/>, like Figma's trimmed text.</summary>
    static double CapTop(Typeface typeface, double middle, FormattedText text) =>
        middle + (typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs.CapsHeight * TextSize : 0) / 2 - text.Baseline;

    static FormattedText Text(string text, Typeface typeface, Brush brush, double dpi) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, TextSize, brush, dpi);
}
