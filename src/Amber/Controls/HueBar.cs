using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Amber.Tint;

namespace Amber.Controls;

/// <summary>
/// Custom's colour bar: the rainbow (hue 0–360) under the level slider, with a handle filled with the picked colour.
/// It works like the level slider: press anywhere and keep dragging, the screen previews it meanwhile, and the double
/// arrow shows over the handle and all through a drag. The handle's centre is the hue.
/// </summary>
public class HueBar : LevelSlider
{
    static readonly DependencyPropertyKey HandleBrushKey = DependencyProperty.RegisterReadOnly(
        nameof(HandleBrush), typeof(Brush), typeof(HueBar), new PropertyMetadata(Brushes.Red));

    public static readonly DependencyProperty HandleBrushProperty = HandleBrushKey.DependencyProperty;

    /// <summary>The picked colour, which fills the handle.</summary>
    public Brush HandleBrush => (Brush)GetValue(HandleBrushProperty);

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        var brush = new SolidColorBrush(ColorOf(newValue));
        brush.Freeze();
        SetValue(HandleBrushKey, brush);
    }

    /// <summary>The colour of a hue (<see cref="TintMatrix.HueColor"/>), for drawing.</summary>
    public static Color ColorOf(double hue)
    {
        var (r, g, b) = TintMatrix.HueColor(hue);
        static byte Byte(double channel) => (byte)Math.Round(channel * 255);
        return Color.FromRgb(Byte(r), Byte(g), Byte(b));
    }

    /// <summary>Whether the pointer at <paramref name="x"/> is on the handle, which is centred where the value puts it.</summary>
    public static bool IsOnHandle(double x, double value, double trackWidth, double handleWidth, double minimum, double maximum)
    {
        var ratio = maximum > minimum ? Math.Clamp((value - minimum) / (maximum - minimum), 0, 1) : 0;
        var centre = handleWidth / 2 + ratio * Math.Max(trackWidth - handleWidth, 0);
        return Math.Abs(x - centre) <= handleWidth / 2;
    }

    protected override bool IsOnGrip(double x, Track track) =>
        IsOnHandle(x, Value, track.ActualWidth, track.Thumb?.ActualWidth ?? 0, Minimum, Maximum);
}
