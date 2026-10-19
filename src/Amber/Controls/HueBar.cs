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
        nameof(HandleBrush), typeof(Brush), typeof(HueBar), new PropertyMetadata(Timeline.Frozen(new SolidColorBrush(ColorOf(0)))));

    public static readonly DependencyProperty HandleBrushProperty = HandleBrushKey.DependencyProperty;

    /// <summary>The picked colour, which fills the handle.</summary>
    public Brush HandleBrush => (Brush)GetValue(HandleBrushProperty);

    // How far (px) from its end the handle has to be before the rainbow's red end under it shows in full.
    const double EndFade = 4;

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        var brush = new SolidColorBrush(ColorOf(newValue));
        brush.Freeze();
        SetValue(HandleBrushKey, brush);
        ShowEnds();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ShowEnds();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ShowEnds();
    }

    /// <summary>
    /// The rainbow's rounded ends are red discs exactly where the handle sits at either end. Drawn on the same edge, the
    /// disc would leak out as a red rim round the handle, so each fades out as the handle reaches it (see Theme.xaml).
    /// </summary>
    void ShowEnds()
    {
        if (GetTemplateChild("PART_Track") is not Track track || GetTemplateChild("StartEnd") is not UIElement start
            || GetTemplateChild("EndEnd") is not UIElement end) return;
        (start.Opacity, end.Opacity) = EndOpacities(Value, track.ActualWidth, track.Thumb?.ActualWidth ?? 0, Minimum, Maximum);
    }

    /// <summary>
    /// How much of the rainbow's start and end discs shows: none while the handle covers that end, all of it once the
    /// handle is <see cref="EndFade"/> px or more away from it.
    /// </summary>
    public static (double Start, double End) EndOpacities(double value, double trackWidth, double handleWidth, double minimum, double maximum)
    {
        var ratio = maximum > minimum ? Math.Clamp((value - minimum) / (maximum - minimum), 0, 1) : 0;
        var travel = Math.Max(trackWidth - handleWidth, 0);
        return (Math.Clamp(ratio * travel / EndFade, 0, 1), Math.Clamp((1 - ratio) * travel / EndFade, 0, 1));
    }

    /// <summary>
    /// The colour a hue shows as wherever the UI stands for the tint (the handle, a filter's dome and curve): what white
    /// turns into at level 100 (<see cref="TintMatrix.CustomColor"/>). Only the bar itself is the pure rainbow.
    /// </summary>
    public static Color ColorOf(double hue)
    {
        var (r, g, b) = TintMatrix.CustomColor(hue);
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
