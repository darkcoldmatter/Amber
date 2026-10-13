using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Amber.Controls;

/// <summary>
/// A dark hint bubble pointing down at a spot on an element (Figma 111:9702): two lines of white bold text over a pointer.
/// It reaches up over the window's header, so it lives in the window's adorner layer instead of the scrolling body.
/// It never takes the mouse.
/// </summary>
public sealed class HintBubble : Adorner
{
    const double BoxHeight = 49, Padding = 12, LineGap = 16, Radius = 8, WindowMargin = 8;
    const double PointerWidth = 9, PointerHeight = 4, PointerRoom = 8; // the pointer stays clear of the rounded corners

    static readonly Brush InkBrush = Timeline.Frozen(new SolidColorBrush(Color.FromRgb(0x27, 0x1F, 0x1F)));

    readonly string[] _lines;
    Point? _tip;

    public HintBubble(UIElement adornedElement, params string[] lines) : base(adornedElement)
    {
        _lines = lines;
        IsHitTestVisible = false;
        Effect = new DropShadowEffect { Color = Color.FromRgb(0x27, 0x1F, 0x1F), Opacity = 0.16, BlurRadius = 30, ShadowDepth = 4, Direction = 270 };
        TextOptions.SetTextRenderingMode(this, TextOptions.GetTextRenderingMode(adornedElement));
        TextOptions.SetTextHintingMode(this, TextOptions.GetTextHintingMode(adornedElement));
    }

    /// <summary>Where the pointer's tip goes, in the adorned element's coordinates; null hides the bubble.</summary>
    public Point? Tip
    {
        get => _tip;
        set
        {
            _tip = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// The bubble is centred over the tip but kept 8 px inside the window; the pointer stays under the tip.
    /// The first line's capitals start 12 px below the top and the last line's baseline sits 12 px above the bottom.
    /// </summary>
    protected override void OnRender(DrawingContext dc)
    {
        if (_tip is not Point tip) return;

        var typeface = new Typeface((FontFamily)FindResource("AppFont"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var lines = _lines.Select(line => new FormattedText(line, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, 12, Brushes.White, dpi)).ToArray();
        var width = Math.Ceiling(lines.Max(line => line.Width)) + 2 * Padding;

        var room = WindowBounds();
        var left = Math.Round(Math.Clamp(tip.X - width / 2, room.Left + WindowMargin, Math.Max(room.Left + WindowMargin, room.Right - WindowMargin - width)));
        var top = Math.Round(tip.Y - PointerHeight - BoxHeight);
        dc.DrawRoundedRectangle(InkBrush, null, new Rect(left, top, width, BoxHeight), Radius, Radius);

        var pointerLeft = Math.Round(Math.Clamp(tip.X - PointerWidth / 2, left + PointerRoom, left + width - PointerRoom - PointerWidth));
        dc.PushTransform(new TranslateTransform(pointerLeft, top + BoxHeight));
        dc.DrawGeometry(InkBrush, null, (Geometry)FindResource("IconHintPointer"));
        dc.Pop();

        var capHeight = typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs.CapsHeight * 12 : 0;
        for (var i = 0; i < lines.Length; i++)
            dc.DrawText(lines[i], new Point(left + Padding, top + Padding + capHeight + i * LineGap - lines[i].Baseline));
    }

    /// <summary>The window's content area, in the adorned element's coordinates.</summary>
    Rect WindowBounds()
    {
        if (Window.GetWindow(AdornedElement)?.Content is not UIElement root) return new Rect(AdornedElement.RenderSize);
        return root.TransformToVisual(AdornedElement).TransformBounds(new Rect(root.RenderSize));
    }
}
