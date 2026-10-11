using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// What the 24-hour timelines share: minutes to pixels, the hour labels, the hour grid,
/// finding a range's edge under the pointer, and the double-arrow cursor shown there.
/// </summary>
public static class Timeline
{
    /// <summary>How close (px) the pointer has to be to a range's edge to grab it.</summary>
    public const double EdgeReach = 6;

    static readonly Brush LabelBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x56, 0x56, 0x56)));
    static readonly Pen SolidLine = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x1A, 0x27, 0x1F, 0x1F)), 1));
    // 3 px dashes, 3 px gaps; the offset starts the pattern 2 px above the track, as in Figma.
    static readonly Pen DashedLine = Frozen(new Pen(SolidLine.Brush, 1) { DashStyle = new DashStyle([3, 3], 2), DashCap = PenLineCap.Flat });

    static Cursor? _resizeCursor;

    /// <summary>The double arrow from Figma, shown only on a range's start or end edge.</summary>
    public static Cursor ResizeCursor => _resizeCursor ??=
        new Cursor(Application.GetResourceStream(new Uri("pack://application:,,,/Assets/resize.cur")).Stream, true);

    public static double X(int minutes, double width) => minutes * width / ScheduleRange.DayMinutes;

    /// <summary>Where a range ends: one that ends at midnight ends at the right-hand end of the timeline.</summary>
    static double EndX(int end, double width) => end == 0 ? width : X(end, width);

    /// <summary>
    /// The pieces of a range as (left, right) pixels: one, or two when it runs past midnight.
    /// A full day that doesn't start at midnight is split where it starts, so that edge stays visible.
    /// </summary>
    public static IEnumerable<(double Left, double Right)> Pieces(int start, int end, double width)
    {
        var startX = X(start, width);
        var endX = EndX(end, width);
        if (startX < endX)
        {
            yield return (startX, endX);
            yield break;
        }
        yield return (startX, width);
        yield return (0, endX);
    }

    /// <summary>Whether a point at <paramref name="x"/> lies within the range.</summary>
    public static bool Contains(double x, int start, int end, double width)
    {
        var length = ScheduleRange.Length(start, end);
        var minutes = x / width * ScheduleRange.DayMinutes;
        return ((minutes - start) % ScheduleRange.DayMinutes + ScheduleRange.DayMinutes) % ScheduleRange.DayMinutes < length;
    }

    /// <summary>
    /// The edge within reach of <paramref name="x"/>: true for the start, false for the end, null for neither.
    /// When both are in reach (a short range), the nearer wins; at the same spot, the one whose side the pointer is on.
    /// </summary>
    public static bool? EdgeAt(double x, int start, int end, double width)
    {
        var startX = X(start, width);
        var endX = EndX(end, width);
        var toStart = Math.Abs(x - startX);
        var toEnd = Math.Abs(x - endX);
        if (toStart > EdgeReach && toEnd > EdgeReach) return null;
        if (toStart > EdgeReach) return false;
        if (toEnd > EdgeReach) return true;
        return toStart == toEnd ? x >= startX : toStart < toEnd;
    }

    /// <summary>"00" to "24" every two hours, centred on their hour; the first and last stay within the track's ends.</summary>
    public static void DrawHourLabels(DrawingContext dc, Rect track, double centerY, FontFamily font, double pixelsPerDip)
    {
        var typeface = new Typeface(font, FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
        var capHeight = typeface.TryGetGlyphTypeface(out var glyphs) ? glyphs.CapsHeight * 12 : 0;
        for (var hour = 0; hour <= 24; hour += 2)
        {
            var text = new FormattedText(hour.ToString("00", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 12, LabelBrush, pixelsPerDip);
            var x = Math.Clamp(track.Left + hour / 24.0 * track.Width - text.Width / 2, track.Left, track.Right - text.Width);
            // Centre the digits themselves (cap height to baseline), like Figma's trimmed text.
            dc.DrawText(text, new Point(x, centerY + capHeight / 2 - text.Baseline));
        }
    }

    /// <summary>A line across the track at every hour: solid on even hours, dashed on odd ones.</summary>
    public static void DrawHourGrid(DrawingContext dc, Rect track)
    {
        for (var hour = 1; hour < 24; hour++)
        {
            var x = Math.Round(track.Left + hour / 24.0 * track.Width) + 0.5; // a crisp 1 px line
            dc.DrawLine(hour % 2 == 0 ? SolidLine : DashedLine, new Point(x, track.Top), new Point(x, track.Bottom));
        }
    }

    /// <summary>Freezes a brush or pen so drawing can share it.</summary>
    internal static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
