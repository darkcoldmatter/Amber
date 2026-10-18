using System.Windows.Media;

namespace Amber;

/// <summary>
/// The look of the wave that runs over the background when the tint is switched on or off: a mosaic of small tiles,
/// laid like bricks, that light up as a ring sweeps out from the window's centre. A first version to tweak: every
/// number of the look is here.
/// </summary>
public static class TileWave
{
    // The tiles: square corners (Radius 0).
    public const double TileWidth = 10, TileHeight = 10, Gap = 2, Radius = 0;
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

    // The tiles float, as on water: the ring lifts them as it passes.
    /// <summary>How much bigger a tile grows on the ring, around its own centre (0.15 = +15 %), easing back like its brightness.</summary>
    public const double LiftScale = 0.15;
    /// <summary>How far (px) a tile is pushed outward from the centre when the ring reaches it.</summary>
    public const double Push = 3;
    /// <summary>After the ring, the push settles in a small damped bob: this many swings a second…</summary>
    public const double BobFrequency = 2.5;
    /// <summary>…shrinking by e every this many seconds.</summary>
    public const double BobDamping = 0.15;
    /// <summary>A shadow under raised tiles: its opacity on the ring (black), and how far (px) it falls below the tile then. 0 = none.</summary>
    public const double ShadowOpacity = 0.06, ShadowDrop = 1.5;

    /// <summary>The cubic ease-out: fast at first, settling at the end.</summary>
    public static double Ease(double progress) => 1 - Math.Pow(1 - Math.Clamp(progress, 0, 1), 3);

    /// <summary>How far (px) the ring has got <paramref name="seconds"/> after it started, reaching <paramref name="farthest"/> at the end.</summary>
    public static double RingRadius(double seconds, double farthest) => farthest * Ease(seconds / Duration);

    /// <summary>
    /// The opacity of a tile <paramref name="distance"/> px from the centre, <paramref name="seconds"/> into the wave:
    /// ahead of the ring it brightens over half the band; from the moment the ring reaches it, it fades out over the trail.
    /// </summary>
    public static double Opacity(double distance, double seconds, double farthest) => PeakOpacity * Swell(distance, seconds, farthest);

    /// <summary>How big a tile is drawn, as a share of its size: 1 at rest, 1 + <see cref="LiftScale"/> on the ring.</summary>
    public static double Scale(double distance, double seconds, double farthest) => 1 + LiftScale * Swell(distance, seconds, farthest);

    /// <summary>
    /// How far (px) a tile is moved outward from the centre (negative: inward). It rises to <see cref="Push"/> as the ring
    /// arrives, then bobs back and forth around rest, dying away (<see cref="BobFrequency"/>, <see cref="BobDamping"/>).
    /// </summary>
    public static double Offset(double distance, double seconds, double farthest)
    {
        if (Since(distance, seconds, farthest) is not { } since) return 0;
        if (since < 0) return Push * Swell(distance, seconds, farthest);
        return since >= Trail ? 0 : Push * Math.Exp(-since / BobDamping) * Math.Cos(2 * Math.PI * BobFrequency * since);
    }

    /// <summary>
    /// The shape every part of the look follows, 0 to 1: ahead of the ring it rises over half the band; from the moment the
    /// ring reaches a tile, it falls away over the trail.
    /// </summary>
    public static double Swell(double distance, double seconds, double farthest)
    {
        if (Since(distance, seconds, farthest) is not { } since) return 0;
        if (since >= 0) return since >= Trail ? 0 : Smooth(1 - since / Trail);
        var ahead = (distance - RingRadius(seconds, farthest)) / (BandWidth / 2);
        return ahead >= 1 ? 0 : Smooth(1 - ahead);
    }

    /// <summary>
    /// Seconds since the ring reached the tile (-1 while it is still on its way), or null when the wave can't touch it:
    /// before or after the wave, or beyond where the ring stops.
    /// </summary>
    static double? Since(double distance, double seconds, double farthest)
    {
        if (seconds < 0 || seconds >= TotalSeconds || farthest <= 0) return null;
        if (distance > RingRadius(seconds, farthest)) return seconds >= Duration ? null : -1;
        // When the ring reached this tile: the inverse of the ease-out.
        return seconds - Duration * (1 - Math.Cbrt(1 - Math.Clamp(distance / farthest, 0, 1)));
    }

    static double Smooth(double x) => x * x * (3 - 2 * x);
}
