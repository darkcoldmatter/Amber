namespace Amber.Tint;

/// <summary>
/// Builds the 5×5 color matrix for the Magnification API.
/// Row-major, input is the row vector [R G B A 1]: element [i, j] is how much input channel i adds to output channel j.
/// </summary>
public static class TintMatrix
{
    // Rec.709 luma weights. Applied to gamma-encoded sRGB, so only approximate — values are tuned by eye.
    static readonly double[] Luma = [0.2126, 0.7152, 0.0722];

    // White turns into (R, G, B). Mono blends toward monochrome: 0 keeps colors, 1 is fully monochrome.
    readonly record struct Look(double R, double G, double B, double Mono);

    // Look at level 0 (weakest) and level 100 (strongest). The level blends between them.
    // Custom has no fixed colour: it is built from the hue the user picked (CustomRange).
    static (Look Weak, Look Strong) Range(TintMode mode, double hue) => mode switch
    {
        TintMode.GoldenHour => (new(1.00, 0.96, 0.88, 0.00), new(1.00, 0.62, 0.12, 0.00)),
        TintMode.Amber => (new(1.00, 0.93, 0.82, 0.00), new(1.00, 0.45, 0.02, 0.85)),
        TintMode.DeepNight => (new(1.00, 0.92, 0.90, 0.15), new(1.00, 0.25, 0.05, 1.00)),
        TintMode.Custom => CustomRange(hue),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    // Custom's look. A first guess, to be tuned by eye on the owner's screen: at level 0 white goes only this far
    // towards the colour (a faint wash), and at level 100 it is the colour itself, as monochrome as Amber's strong
    // look so text stays readable whatever the hue.
    const double CustomWeakAmount = 0.15;
    const double CustomWeakMono = 0.00;
    const double CustomStrongMono = 0.85;

    static (Look Weak, Look Strong) CustomRange(double hue)
    {
        var (r, g, b) = HueColor(hue);
        double Weak(double c) => 1 + CustomWeakAmount * (c - 1);
        return (new(Weak(r), Weak(g), Weak(b), CustomWeakMono), new(r, g, b, CustomStrongMono));
    }

    /// <summary>
    /// The colour of a hue on the colour bar, at full saturation and brightness (HSV with S = V = 1), each channel 0–1:
    /// 0 red, 60 yellow, 120 green, 180 cyan, 240 blue, 300 magenta, and 360 red again.
    /// </summary>
    public static (double R, double G, double B) HueColor(double hue)
    {
        var h = (hue % 360 + 360) % 360 / 60;   // 0 to 6: one step per sixth of the bar
        double Channel(double n)
        {
            var k = (n + h) % 6;
            return 1 - Math.Clamp(Math.Min(k, 4 - k), 0, 1);
        }
        return (Channel(5), Channel(3), Channel(1));
    }

    public static float[] Identity =>
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
        0, 0, 0, 0, 1,
    ];

    /// <summary>
    /// True when two matrices differ by enough to be worth showing: at least a quarter of one 8-bit colour step (1/255)
    /// somewhere. Small enough that even the faintest look (Golden hour at level 0) still fades in steps of 1 %.
    /// </summary>
    public static bool Differs(float[] a, float[] b)
    {
        for (var k = 0; k < a.Length; k++)
            if (Math.Abs(a[k] - b[k]) >= 0.001f) return true;
        return false;
    }

    /// <summary>
    /// Mixes several looks by weight, filling any leftover weight with an untinted screen.
    /// Used where two filters overlap while one fades out and the next fades in.
    /// </summary>
    public static float[] Mix(IEnumerable<(float[] Matrix, double Weight)> looks)
    {
        var identity = Identity;
        var mixed = Identity;
        var used = 0.0;
        foreach (var (matrix, weight) in looks)
        {
            if (weight <= 0) continue;
            used += weight;
            for (var k = 0; k < mixed.Length; k++) mixed[k] += (float)((matrix[k] - identity[k]) * weight);
        }

        // More than a full weight means two looks overlap: share them out instead of piling up.
        if (used <= 1) return mixed;

        for (var k = 0; k < mixed.Length; k++) mixed[k] = (float)(identity[k] + (mixed[k] - identity[k]) / used);
        return mixed;
    }

    /// <summary>
    /// The mode's look at <paramref name="level"/>, mixed with "no tint at all" by <paramref name="strength"/>
    /// (0 = untinted screen, 1 = the full look). Fades run on this, not on the level: level 0 is already a visible tint.
    /// </summary>
    /// <param name="hue">Custom's colour, 0–360 (<see cref="HueColor"/>); the other tints ignore it.</param>
    public static float[] Blend(TintMode mode, double level, double strength, double hue = 0)
    {
        var t = Math.Clamp(strength, 0, 1);
        var identity = Identity;
        var target = Build(mode, level, hue);
        for (var k = 0; k < target.Length; k++) target[k] = (float)(identity[k] + (target[k] - identity[k]) * t);
        return target;
    }

    /// <param name="level">0 (weakest) to 100 (strongest).</param>
    /// <param name="hue">Custom's colour, 0–360 (<see cref="HueColor"/>); the other tints ignore it.</param>
    public static float[] Build(TintMode mode, double level, double hue = 0)
    {
        var t = Math.Clamp(level, 0, 100) / 100;
        var (weak, strong) = Range(mode, hue);
        double Lerp(double a, double b) => a + (b - a) * t;

        double[] color = [Lerp(weak.R, strong.R), Lerp(weak.G, strong.G), Lerp(weak.B, strong.B)];
        var mono = Lerp(weak.Mono, strong.Mono);

        // output[j] = color[j] * ((1 - mono) * input[j] + mono * luma)
        var m = Identity;
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
                m[i * 5 + j] = (float)(color[j] * ((i == j ? 1 - mono : 0) + mono * Luma[i]));
        return m;
    }
}
