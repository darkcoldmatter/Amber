using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// The simple schedule's 24-hour timeline: hour labels over a tan track with one white range.
/// Either end of the range can be dragged; it snaps to 15 minutes and wraps past midnight,
/// so the range splits in two and dragging continues from the other side.
/// </summary>
public class ScheduleBar : FrameworkElement
{
    const double TrackTop = 32, TrackHeight = 42, TrackRadius = 14, LabelsAbove = 15.5;
    const double RangeInset = 2, RangeRadius = 12, OutlineWidth = 1.5;

    static readonly Brush TrackBrush = Timeline.Frozen(new SolidColorBrush(Color.FromArgb(0x29, 0xAF, 0x58, 0x00)));
    static readonly Pen OutlinePen = Timeline.Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x4D, 0x27, 0x1F, 0x1F)), OutlineWidth));

    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
        nameof(Start), typeof(TimeOnly), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(new TimeOnly(20, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(
        nameof(End), typeof(TimeOnly), typeof(ScheduleBar),
        new FrameworkPropertyMetadata(new TimeOnly(8, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    bool? _dragging; // the edge being dragged: true = start, false = end
    bool _hovered;

    public TimeOnly Start { get => (TimeOnly)GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public TimeOnly End { get => (TimeOnly)GetValue(EndProperty); set => SetValue(EndProperty, value); }

    int StartMinutes => ScheduleRange.Minutes(Start);
    int EndMinutes => ScheduleRange.Minutes(End);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);

        if (_dragging is bool movingStart)
        {
            var (start, end) = ScheduleRange.MoveEdge(StartMinutes, EndMinutes, movingStart, ScheduleRange.MinutesAt(point.X, ActualWidth));
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

        _dragging = isStart;
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

        foreach (var (left, right) in Timeline.Pieces(StartMinutes, EndMinutes, ActualWidth))
        {
            // White range, 2 px inside the track and on whole pixels so its sides stay sharp.
            var range = new Rect(Math.Round(left) + RangeInset, track.Top + RangeInset,
                Math.Max(Math.Round(right) - Math.Round(left) - 2 * RangeInset, 1), TrackHeight - 2 * RangeInset);
            dc.DrawRoundedRectangle(Brushes.White, null, range, RangeRadius, RangeRadius);
            if (!_hovered) continue;

            // The outline sits inside the range, like a Figma inside stroke.
            range.Inflate(-OutlineWidth / 2, -OutlineWidth / 2);
            dc.DrawRoundedRectangle(null, OutlinePen, range, RangeRadius - OutlineWidth / 2, RangeRadius - OutlineWidth / 2);
        }
    }
}
