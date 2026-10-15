using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Amber;

/// <summary>
/// How all text is drawn: smoothed in grey (ClearType adds colour fringes) and not snapped to the pixel grid
/// ("Animated" hinting), which is how Figma draws it; grey text snapped to the grid looks jagged at 100 % scaling.
/// Made the default once at startup, before any window or menu exists, so nothing has to set it again.
/// </summary>
public static class TextLook
{
    public static void MakeDefault()
    {
        // WPF takes the hinting from each element itself rather than inheriting it, so every element, menus included,
        // gets it by default.
        TextOptions.TextHintingModeProperty.OverrideMetadata(typeof(FrameworkElement), new UIPropertyMetadata(TextHintingMode.Animated));

        // Grey smoothing is inherited, and WPF hands a popup's own to everything it shows. (A window passes it on only
        // when it is set on the window itself, which is why MainWindow sets it.)
        TextOptions.TextRenderingModeProperty.OverrideMetadata(typeof(Popup), new FrameworkPropertyMetadata(TextRenderingMode.Grayscale));
    }
}
