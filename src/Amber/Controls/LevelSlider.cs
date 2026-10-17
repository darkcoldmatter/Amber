using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Amber.Controls;

/// <summary>
/// A slider you can press anywhere and keep dragging, not only by its thumb. Over its grip, and while it is dragged,
/// the pointer shows the timelines' double arrow.
/// </summary>
public class LevelSlider : Slider
{
    const double PastFill = 3; // how far past the end of the white fill the grip still reaches

    /// <summary>Raised while the user drags, so the screen can show the level being set.</summary>
    public event EventHandler? PreviewStarted;
    public event EventHandler? PreviewEnded;

    bool _previewing;

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        _previewing = true;
        PreviewStarted?.Invoke(this, EventArgs.Empty);
        MoveTo(e);
        ShowCursor(e);
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (IsMouseCaptured) MoveTo(e);
        ShowCursor(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        EndPreview();
        ShowCursor(e);
    }

    /// <summary>Losing the mouse mid-drag (Alt+Tab, a window popping up) must end the preview too, or the tint would stay.</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndPreview();
    }

    void EndPreview()
    {
        if (!_previewing) return;
        _previewing = false;
        PreviewEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The value whose thumb center sits at <paramref name="x"/>. Depends only on the pointer position,
    /// never on the current value, so repeated clicks on one spot always give the same result.
    /// </summary>
    public static double ValueAt(double x, double trackWidth, double thumbWidth, double minimum, double maximum)
    {
        var travel = trackWidth - thumbWidth;
        if (travel <= 0) return minimum;
        var ratio = Math.Clamp((x - thumbWidth / 2) / travel, 0, 1);
        return minimum + ratio * (maximum - minimum);
    }

    /// <summary>
    /// Whether the pointer at <paramref name="x"/> is on the grip: from 6 px left of the grip line, which sits in the
    /// middle of the thumb (where <see cref="ValueAt"/> puts it for <paramref name="value"/>), to 3 px past the end of the
    /// white fill, the thumb's right edge.
    /// </summary>
    public static bool IsOnGrip(double x, double value, double trackWidth, double thumbWidth, double minimum, double maximum)
    {
        var ratio = maximum > minimum ? Math.Clamp((value - minimum) / (maximum - minimum), 0, 1) : 0;
        var grip = thumbWidth / 2 + ratio * Math.Max(trackWidth - thumbWidth, 0);
        return x >= grip - Timeline.EdgeReach && x <= grip + thumbWidth / 2 + PastFill;
    }

    void MoveTo(MouseEventArgs e)
    {
        if (GetTemplateChild("PART_Track") is not Track track) return;
        Value = ValueAt(e.GetPosition(track).X, track.ActualWidth, track.Thumb?.ActualWidth ?? 0, Minimum, Maximum);
    }

    /// <summary>The double arrow over the grip and all through a drag (the grip follows the pointer); the arrow elsewhere.</summary>
    void ShowCursor(MouseEventArgs e)
    {
        if (GetTemplateChild("PART_Track") is not Track track) return;
        var onGrip = IsOnGrip(e.GetPosition(track).X, Value, track.ActualWidth, track.Thumb?.ActualWidth ?? 0, Minimum, Maximum);
        Cursor = IsMouseCaptured || onGrip ? Timeline.ResizeCursor : Cursors.Arrow;
    }
}
