using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// The 24-hour scale and the schedule bar. Either edge can be dragged; edges snap to 15 minutes and
/// wrap past midnight, so the bar splits in two and dragging continues from the other side.
/// </summary>
public class ScheduleBar : FrameworkElement
{
    public const int SnapMinutes = 15;
    public const int DayMinutes = 24 * 60;

    const double BarTop = 34, BarHeight = 32, CornerRadius = 6, GrabWidth = 8;
    const double LabelCenterY = 10.5, TickBottom = 28, LongTick = 9, ShortTick = 6;

    static readonly Brush TrackBrush = Frozen("#F5F3EE");
    static readonly Brush TickBrush = Frozen("#D3D6DF");
    static readonly Brush LabelBrush = Frozen("#565656");

    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
        nameof(Start), typeof(TimeOnly), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(new TimeOnly(20, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(
        nameof(End), typeof(TimeOnly), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(new TimeOnly(8, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(Frozen("#FEC960"), FrameworkPropertyMetadataOptions.AffectsRender));

    bool _draggingStart;
    bool _dragging;

    public TimeOnly Start { get => (TimeOnly)GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public TimeOnly End { get => (TimeOnly)GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <summary>Minutes since midnight under the pointer, snapped and wrapped into one day.</summary>
    public static int MinutesAt(double x, double width)
    {
        var raw = (int)Math.Round(x / width * DayMinutes / SnapMinutes) * SnapMinutes;
        return ((raw % DayMinutes) + DayMinutes) % DayMinutes;
    }

    /// <summary>Length of a range in minutes; start == end means the whole day.</summary>
    public static int Length(int start, int end)
    {
        var length = ((end - start) % DayMinutes + DayMinutes) % DayMinutes;
        return length == 0 ? DayMinutes : length;
    }

    /// <summary>
    /// Moves one edge to <paramref name="target"/>, keeping the range between 15 minutes and a full day.
    /// An edge never jumps over the other one — that would turn a short range into an almost-full day in one step;
    /// dragging the long way round is how you fill the whole day.
    /// </summary>
    public static (int Start, int End) MoveEdge(int start, int end, bool movingStart, int target)
    {
        var current = Length(start, end);
        var wanted = movingStart ? Length(target, end) : Length(start, target);
        if (Math.Abs(wanted - current) > DayMinutes / 2) return (start, end);

        var length = Math.Clamp(wanted, SnapMinutes, DayMinutes);
        return movingStart
            ? (((end - length) % DayMinutes + DayMinutes) % DayMinutes, end)
            : (start, (start + length) % DayMinutes);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.GetPosition(this).X;

        if (_dragging)
        {
            var (start, end) = MoveEdge(Minutes(Start), Minutes(End), _draggingStart, MinutesAt(x, ActualWidth));
            Start = Time(start);
            End = Time(end);
            return;
        }

        Cursor = EdgeAt(x) is null ? Cursors.Arrow : Cursors.SizeWE;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (EdgeAt(e.GetPosition(this).X) is not bool isStart) return;

        _dragging = true;
        _draggingStart = isStart;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragging = false;
        ReleaseMouseCapture();
    }

    /// <summary>True when the pointer is near the start edge, false for the end edge, null when it is near neither.</summary>
    bool? EdgeAt(double x)
    {
        var startX = Minutes(Start) / (double)DayMinutes * ActualWidth;
        var endX = Minutes(End) / (double)DayMinutes * ActualWidth;
        if (Math.Abs(x - startX) <= GrabWidth) return true;
        if (Math.Abs(x - endX) <= GrabWidth) return false;
        return null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var hourWidth = width / 24;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);

        for (var hour = 0; hour <= 24; hour++)
        {
            var x = Math.Round(hour * hourWidth) + 0.5;
            var isLabelled = hour % 2 == 0;
            dc.DrawLine(new Pen(TickBrush, 1), new Point(x, TickBottom - (isLabelled ? LongTick : ShortTick)), new Point(x, TickBottom));
            if (!isLabelled) continue;

            var text = new FormattedText(hour.ToString("00", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, LabelBrush, dpi);
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 0, width - text.Width), LabelCenterY - text.Height / 2));
        }

        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(0, BarTop, width, BarHeight), CornerRadius, CornerRadius);

        var start = Minutes(Start);
        var end = Minutes(End);
        if (Length(start, end) == DayMinutes)
        {
            DrawSegment(dc, 0, width);
        }
        else if (start < end)
        {
            DrawSegment(dc, start / (double)DayMinutes * width, end / (double)DayMinutes * width);
        }
        else
        {
            DrawSegment(dc, start / (double)DayMinutes * width, width);
            DrawSegment(dc, 0, end / (double)DayMinutes * width);
        }

        DrawGrip(dc, start / (double)DayMinutes * width, isStart: true);
        DrawGrip(dc, end / (double)DayMinutes * width, isStart: false);
    }

    void DrawSegment(DrawingContext dc, double left, double right) =>
        dc.DrawRoundedRectangle(Accent, null, new Rect(left, BarTop, Math.Max(right - left, 1), BarHeight), CornerRadius, CornerRadius);

    /// <summary>The white handle line, tucked 4 px inside its edge of the bar.</summary>
    void DrawGrip(DrawingContext dc, double edgeX, bool isStart)
    {
        var x = isStart ? edgeX + 4 : edgeX - 7;
        x = Math.Clamp(x, 0, Math.Max(ActualWidth - 3, 0));
        dc.DrawRoundedRectangle(Brushes.White, null, new Rect(x, BarTop + 8, 3, 16), 1.5, 1.5);
    }

    static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;
    static TimeOnly Time(int minutes) => new(minutes / 60 % 24, minutes % 60);

    static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }
}
