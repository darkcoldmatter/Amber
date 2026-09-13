using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Amber.Controls;

/// <summary>A slider you can press anywhere and keep dragging, not only by its thumb.</summary>
public class LevelSlider : Slider
{
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
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
    }

    void MoveTo(MouseEventArgs e)
    {
        if (GetTemplateChild("PART_Track") is Track track)
            Value = track.ValueFromPoint(e.GetPosition(track));
    }
}
