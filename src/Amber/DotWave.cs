using System.Windows.Media;

namespace Amber;

/// <summary>
/// The look of the wave that runs over the background when the tint is switched on or off: a halftone screen of tiny
/// square dots, invisible at rest, that light up as a ring sweeps out from the window's centre while the ring bends the
/// screen like a ripple on water. A first version to tweak: every number of the look is here.
/// </summary>
public static class DotWave
{
    // The screen of dots.
    /// <summary>Distance (px) from one dot to the next.</summary>
    public const double Pitch = 8;
    /// <summary>Side (px) of a dot; square, not rounded.</summary>
    public const double DotSize = 3.25;
    /// <summary>The classic screentone angle: the grid turned 45°, same spacing. False: a plain square grid.</summary>
    public const bool Screen45 = false;
    /// <summary>Each dot can keep its own size, up to this much bigger or smaller than <see cref="DotSize"/> (0.4 = ±40 %; 0 = all one size)…</summary>
    public const double SizeJitter = 0;
    /// <summary>…and its own strength, between this share of the peak and the full peak.</summary>
    public const double FaintestDot = 0.4;
    /// <summary>The seed of each dot's size and strength, so the screen is the same every time and never flickers.</summary>
    public const int Seed = 7;
    public static readonly Color DotColor = Colors.White;

    // The ring.
    /// <summary>Seconds the ring takes from the centre to the farthest corner, slowing down as it goes (see <see cref="EasePower"/>).</summary>
    public const double Duration = 1.5;
    /// <summary>How hard the ring slows down: 3 = cubic ease-out, higher = a faster start and a longer, slower finish.</summary>
    public const double EasePower = 5;
    /// <summary>How wide (px) the lit band around the ring is: full at its centre line, nothing at its edges.</summary>
    public const double BandWidth = 80;
    /// <summary>Opacity of the strongest dots right on the ring.</summary>
    public const double PeakOpacity = 0.8;
    /// <summary>Seconds a dot takes to fade back to nothing once the ring has passed it.</summary>
    public const double Trail = 0.45;

    // The ripple: the ring bends the screen, pushing dots ahead of it outward and those just behind it inward.
    /// <summary>How far (px) the ripple moves a dot at most, along its line from the centre.</summary>
    public const double WarpAmplitude = 3;
    /// <summary>The ripple's wavelength (px): one swing out and back in across this distance.</summary>
    public const double WarpWavelength = 60;
    /// <summary>Behind the ring the ripple dies away, by e every this many px.</summary>
    public const double WarpTail = 40;

    /// <summary>How long the whole wave lasts, its trail included.</summary>
    public const double TotalSeconds = Duration + Trail;

    /// <summary>The ease-out: fast at first, settling at the end.</summary>
    public static double Ease(double progress) => 1 - Math.Pow(1 - Math.Clamp(progress, 0, 1), EasePower);

    /// <summary>How far (px) the ring has got <paramref name="seconds"/> after it started, reaching <paramref name="farthest"/> at the end.</summary>
    public static double RingRadius(double seconds, double farthest) => farthest * Ease(seconds / Duration);

    /// <summary>The opacity of a full-strength dot <paramref name="distance"/> px from the centre, <paramref name="seconds"/> into the wave.</summary>
    public static double Opacity(double distance, double seconds, double farthest) => PeakOpacity * Swell(distance, seconds, farthest);

    /// <summary>
    /// How lit a dot is, 0 to 1: ahead of the ring it brightens over half the band; from the moment the ring reaches it,
    /// it fades out over the trail.
    /// </summary>
    public static double Swell(double distance, double seconds, double farthest)
    {
        if (seconds < 0 || seconds >= TotalSeconds || farthest <= 0) return 0;

        var ring = RingRadius(seconds, farthest);
        if (distance > ring)
        {
            if (seconds >= Duration) return 0; // the ring has stopped short of it
            var ahead = (distance - ring) / (BandWidth / 2);
            return ahead >= 1 ? 0 : Smooth(1 - ahead);
        }

        // When the ring reached this dot: the inverse of the ease-out.
        var reached = Duration * (1 - Math.Pow(1 - Math.Clamp(distance / farthest, 0, 1), 1 / EasePower));
        var since = (seconds - reached) / Trail;
        return since >= 1 ? 0 : Smooth(1 - since);
    }

    /// <summary>
    /// How far (px) the ripple moves a dot outward from the centre (negative: inward): a sine across the ring, zero on its
    /// centre line, outward ahead of it and inward behind; fading out over half the band ahead, dying away behind
    /// (<see cref="WarpTail"/>), and easing to rest as the wave ends.
    /// </summary>
    public static double Warp(double distance, double seconds, double farthest)
    {
        if (seconds < 0 || seconds >= TotalSeconds || farthest <= 0) return 0;

        var past = distance - RingRadius(seconds, farthest); // > 0 ahead of the ring
        var envelope = past > 0 ? (past >= BandWidth / 2 ? 0 : Smooth(1 - past / (BandWidth / 2))) : Math.Exp(past / WarpTail);
        var ending = seconds <= Duration ? 1 : Smooth(1 - (seconds - Duration) / Trail);
        return WarpAmplitude * Math.Sin(2 * Math.PI * past / WarpWavelength) * envelope * ending;
    }

    /// <summary>A dot's own size, as a share of <see cref="DotSize"/>, and its own strength, as a share of the peak; fixed by where it sits.</summary>
    public static (double Size, double Strength) Character(int column, int row)
    {
        var hash = Hash(column, row);
        var size = 1 + SizeJitter * (2 * Fraction(hash) - 1);
        var strength = FaintestDot + (1 - FaintestDot) * Fraction(Hash((int)hash, Seed));
        return (size, strength);
    }

    /// <summary>A well-mixed number from a dot's place and the seed (a small integer hash), the same every time.</summary>
    static uint Hash(int column, int row) => unchecked(Mix((uint)Seed * 0x9E3779B9u ^ (uint)column * 0x85EBCA6Bu ^ (uint)row * 0xC2B2AE35u));

    static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            return h ^ (h >> 16);
        }
    }

    static double Fraction(uint hash) => (hash & 0xFFFFFF) / (double)0x1000000;

    static double Smooth(double x) => x * x * (3 - 2 * x);
}
