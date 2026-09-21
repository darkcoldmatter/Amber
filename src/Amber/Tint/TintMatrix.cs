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
    static (Look Weak, Look Strong) Range(TintMode mode) => mode switch
    {
        TintMode.GoldenHour => (new(1.00, 0.96, 0.88, 0.00), new(1.00, 0.62, 0.12, 0.00)),
        TintMode.Amber => (new(1.00, 0.93, 0.82, 0.00), new(1.00, 0.45, 0.02, 0.85)),
        TintMode.Astronomic => (new(1.00, 0.92, 0.90, 0.15), new(1.00, 0.25, 0.05, 1.00)),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static float[] Identity =>
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
        0, 0, 0, 0, 1,
    ];

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
    public static float[] Blend(TintMode mode, double level, double strength)
    {
        var t = Math.Clamp(strength, 0, 1);
        var identity = Identity;
        var target = Build(mode, level);
        for (var k = 0; k < target.Length; k++) target[k] = (float)(identity[k] + (target[k] - identity[k]) * t);
        return target;
    }

    /// <param name="level">0 (weakest) to 100 (strongest).</param>
    public static float[] Build(TintMode mode, double level)
    {
        var t = Math.Clamp(level, 0, 100) / 100;
        var (weak, strong) = Range(mode);
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
