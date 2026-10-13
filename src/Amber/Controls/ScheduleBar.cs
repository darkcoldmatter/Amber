using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// The simple schedule's 24-hour timeline: hour labels over a tan track with one white range, which has a grip
/// line near each end. Either end of the range can be dragged; it snaps to 15 minutes and wraps past midnight,
/// so the range splits in two and dragging continues from the other side.
/// </summary>
public class ScheduleBar : FrameworkElement
{
    const double TrackTop = 32, TrackHeight = 42, TrackRadius = 14, LabelsAbove = 15.5;
    const double RangeInset = 2, RangeRadius = 12, OutlineWidth = 1.5;
    // Grip lines as on the level slider: 2 × 14, 8 px inside the edge, and at least 4 px of white beside them.
    const double GripInset = 8, GripWidth = 2, GripHeight = 14, GripRoom = 4;

    static readonly Brush TrackBrush = Timeline.Frozen(new SolidColorBrush(Color.FromArgb(0x29, 0xAF, 0x58, 0x00)));
    static readonly Brush GripBrush = Timeline.Frozen(new SolidColorBrush(Color.FromArgb(0x33, 0x27, 0x1F, 0x1F)));
    static readonly Pen OutlinePen = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x4D, 0x27, 0x1F, 0x1F)), OutlineWidth));

    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
        nameof(Start), typeof(TimeOnly), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(new TimeOnly(20, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(
        nameof(End), typeof(TimeOnly), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(new TimeOnly(8, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    bool? _dragging; // the edge being dragged: true = start, false = end
    // Where that edge was when it was grabbed, where the pointer was then, and whether it has since moved more than a nudge.
    int _grabbedEdge;
    double _pressX;
    bool _moved;
    bool _hovered;

    public TimeOnly Start { get => (TimeOnly)GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public TimeOnly End { get => (TimeOnly)GetValue(EndProperty); set => SetValue(EndProperty, value); }

    int StartMinutes => ScheduleRange.Minutes(Start);
    int EndMinutes => ScheduleRange.Minutes(End);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);

        // Once the pointer has moved more than a nudge, the edge travels with it from where it was grabbed, so it never jumps.
        if (_dragging is bool movingStart)
        {
            if (!_moved && Math.Abs(point.X - _pressX) < SystemParameters.MinimumHorizontalDragDistance) return;
            _moved = true;
            var target = Timeline.DragTarget(_grabbedEdge, movingStart, _pressX, point.X, ActualWidth);
            var (start, end) = ScheduleRange.MoveEdge(StartMinutes, EndMinutes, movingStart, target);
            Start = ScheduleRange.Time(start);
            End = ScheduleRange.Time(end);
            return;
        }

        var edge = OnTrack(point) ? Timeline.EdgeAt(point.X, StartMinutes, EndMinutes, ActualWidth) : null;
        Cursor = edge is null ? Cursors.Arrow : Timeline.ResizeCursor;
        SetHovered(edge is not null || (OnTrack(point) && Timeline.Contains(point.X, StartMinutes, EndMinutes, ActualWidth)));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragging is null) SetHovered(false);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var point = e.GetPosition(this);
        if (!OnTrack(point) || Timeline.EdgeAt(point.X, StartMinutes, EndMinutes, ActualWidth) is not bool isStart) return;
        if (!CaptureMouse()) return; // without the mouse, a drag could never end

        (_dragging, _grabbedEdge, _pressX, _moved) = (isStart, isStart ? StartMinutes : EndMinutes, point.X, false);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging is not null) ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragging = null;
    }

    static bool OnTrack(Point point) => point.Y >= TrackTop && point.Y <= TrackTop + TrackHeight;

    /// <summary>Hovering anywhere on the range outlines it, at once (nothing on the timeline animates).</summary>
    void SetHovered(bool hovered)
    {
        if (hovered == _hovered) return;
        _hovered = hovered;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var track = new Rect(0, TrackTop, ActualWidth, TrackHeight);
        Timeline.DrawHourLabels(dc, track, TrackTop - LabelsAbove, (FontFamily)FindResource("NumberFont"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawRoundedRectangle(TrackBrush, null, track, TrackRadius, TrackRadius);
        Timeline.DrawHourGrid(dc, track);

        // White range, 2 px inside the track and on whole pixels so its sides stay sharp: one piece, or two past midnight.
        var pieces = Timeline.Pieces(StartMinutes, EndMinutes, ActualWidth)
            .Select(piece => new Rect(Math.Round(piece.Left) + RangeInset, track.Top + RangeInset,
                Math.Max(Math.Round(piece.Right) - Math.Round(piece.Left) - 2 * RangeInset, 1), TrackHeight - 2 * RangeInset))
            .ToArray();
        foreach (var range in pieces) dc.DrawRoundedRectangle(Brushes.White, null, range, RangeRadius, RangeRadius);
        DrawGrips(dc, pieces);
        if (!_hovered) return;

        // The outline sits inside the range, like a Figma inside stroke.
        foreach (var range in pieces)
        {
            var outline = range;
            outline.Inflate(-OutlineWidth / 2, -OutlineWidth / 2);
            dc.DrawRoundedRectangle(null, OutlinePen, outline, RangeRadius - OutlineWidth / 2, RangeRadius - OutlineWidth / 2);
        }
    }

    /// <summary>
    /// One grip line just inside the range's start and one inside its end; never at the midnight cut, where the
    /// range only looks split. The first piece always begins at the start and the last one ends at the end.
    /// If either line would be cramped, there are none.
    /// </summary>
    static void DrawGrips(DrawingContext dc, Rect[] pieces)
    {
        var first = pieces[0];
        var last = pieces[^1];
        var span = GripInset + GripWidth;
        var fits = pieces.Length == 1
            ? first.Width >= 2 * span + GripRoom
            : first.Width >= span + GripRoom && last.Width >= span + GripRoom;
        if (!fits) return;

        var top = first.Top + (first.Height - GripHeight) / 2;
        dc.DrawRoundedRectangle(GripBrush, null, new Rect(first.Left + GripInset, top, GripWidth, GripHeight), 1, 1);
        dc.DrawRoundedRectangle(GripBrush, null, new Rect(last.Right - GripInset - GripWidth, top, GripWidth, GripHeight), 1, 1);
    }
}
