using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// One line of letter-spaced text, since WPF text has no tracking. Its box is Figma's trimmed text box,
/// cap height down to baseline, so centring the control centres the letters themselves.
/// The letters are drawn as shapes, which keeps big numbers as smooth as in Figma.
/// </summary>
public class TrackedText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TrackedText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Extra space between letters in pixels; negative pulls them together.</summary>
    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(
        nameof(Tracking), typeof(double), typeof(TrackedText),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(typeof(TrackedText),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily, Affects));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(typeof(TrackedText),
        new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, Affects));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(typeof(TrackedText),
        new FrameworkPropertyMetadata(FontWeights.Normal, Affects));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(TrackedText),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    const FrameworkPropertyMetadataOptions Affects =
        FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

    Geometry? _letters;

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Tracking { get => (double)GetValue(TrackingProperty); set => SetValue(TrackingProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        _letters = null;
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
        if (string.IsNullOrEmpty(Text) || !typeface.TryGetGlyphTypeface(out var font)) return default;

        var indices = new ushort[Text.Length];
        var advances = new double[Text.Length];
        var width = 0.0;
        for (var i = 0; i < Text.Length; i++)
        {
            indices[i] = font.CharacterToGlyphMap.TryGetValue(Text[i], out var glyph) ? glyph : (ushort)0;
            // Tracking goes between letters, not after the last one, so the text stays centred.
            advances[i] = font.AdvanceWidths[indices[i]] * FontSize + (i < Text.Length - 1 ? Tracking : 0);
            width += advances[i];
        }

        var capHeight = font.CapsHeight * FontSize;
        _letters = new GlyphRun(font, 0, false, FontSize, (float)VisualTreeHelper.GetDpi(this).PixelsPerDip, indices,
            new Point(0, capHeight), advances, null, null, null, null, null, null).BuildGeometry();
        _letters.Freeze();
        return new Size(width, capHeight);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_letters is not null) dc.DrawGeometry(Foreground, null, _letters);
    }
}
