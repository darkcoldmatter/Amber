using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Amber.Controls;

/// <summary>A slider you can press anywhere and keep dragging, not only by its thumb.</summary>
public class LevelSlider : Slider
{
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
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (IsMouseCaptured) MoveTo(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        ReleaseMouseCapture();
        EndPreview();
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

    void MoveTo(MouseEventArgs e)
    {
        if (GetTemplateChild("PART_Track") is not Track track) return;
        Value = ValueAt(e.GetPosition(track).X, track.ActualWidth, track.Thumb?.ActualWidth ?? 0, Minimum, Maximum);
    }
}
