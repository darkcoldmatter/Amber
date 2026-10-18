using System.Windows.Media;

namespace Amber;

/// <summary>
/// The look of the wave that runs over the background when the tint is switched on or off: a mosaic of small tiles,
/// laid like bricks, that light up as a ring sweeps out from the window's centre. A first version to tweak: every
/// number of the look is here.
/// </summary>
public static class TileWave
{
    // The tiles.
    public const double TileWidth = 10, TileHeight = 10, Gap = 2, Radius = 2;
    /// <summary>Every other row shifted by half a tile, like bricks; false lines them up in a grid.</summary>
    public const bool Brick = true;
    public static readonly Color TileColor = Colors.White;

    // The ring.
    /// <summary>Seconds the ring takes from the centre to the farthest corner, slowing down as it goes (cubic ease-out).</summary>
    public const double Duration = 0.8;
    /// <summary>How wide (px) the lit band around the ring is: full at its centre line, nothing at its edges.</summary>
    public const double BandWidth = 80;
    /// <summary>Opacity of a tile right on the ring.</summary>
    public const double PeakOpacity = 0.5;
    /// <summary>Seconds a tile takes to fade back to nothing once the ring has passed it.</summary>
    public const double Trail = 0.3;

    /// <summary>How long the whole wave lasts, its trail included.</summary>
    public const double TotalSeconds = Duration + Trail;

    /// <summary>The cubic ease-out: fast at first, settling at the end.</summary>
    public static double Ease(double progress) => 1 - Math.Pow(1 - Math.Clamp(progress, 0, 1), 3);

    /// <summary>How far (px) the ring has got <paramref name="seconds"/> after it started, reaching <paramref name="farthest"/> at the end.</summary>
    public static double RingRadius(double seconds, double farthest) => farthest * Ease(seconds / Duration);

    /// <summary>
    /// The opacity of a tile <paramref name="distance"/> px from the centre, <paramref name="seconds"/> into the wave:
    /// ahead of the ring it brightens over half the band; from the moment the ring reaches it, it fades out over the trail.
    /// </summary>
    public static double Opacity(double distance, double seconds, double farthest)
    {
        if (seconds < 0 || seconds >= TotalSeconds || farthest <= 0) return 0;

        var ring = RingRadius(seconds, farthest);
        if (distance > ring)
        {
            if (seconds >= Duration) return 0; // the ring has stopped short of it
            var ahead = (distance - ring) / (BandWidth / 2);
            return ahead >= 1 ? 0 : PeakOpacity * Smooth(1 - ahead);
        }

        // When the ring reached this tile: the inverse of the ease-out.
        var reached = Duration * (1 - Math.Cbrt(1 - Math.Clamp(distance / farthest, 0, 1)));
        var since = (seconds - reached) / Trail;
        return since >= 1 ? 0 : PeakOpacity * Smooth(1 - since);
    }

    static double Smooth(double x) => x * x * (3 - 2 * x);
}
