namespace Amber;

/// <summary>
/// The rules behind a filter's curve: points sit on 15-minute marks, the first and last stay at the ends
/// of the range, and a point nudged almost level with its neighbour snaps to it.
/// </summary>
public static class CurveMath
{
    public const int StepMinutes = ScheduleRange.SnapMinutes;

    /// <summary>A point within ~3 levels of a neighbour lines up with it exactly.</summary>
    public const double LevelSnap = 3;

    public static int SnapTime(double minutes, int length) =>
        Math.Clamp((int)Math.Round(minutes / StepMinutes) * StepMinutes, 0, length);

    public static double SnapLevel(double level, IEnumerable<double> neighbours)
    {
        var snapped = Math.Clamp(level, 0, 100);
        foreach (var neighbour in neighbours)
            if (Math.Abs(neighbour - snapped) <= LevelSnap) return neighbour;
        return snapped;
    }

    /// <summary>A curve that hasn't been drawn yet: a flat line across the range.</summary>
    public static List<CurvePoint> Flat(int length, double level) => [new(0, level), new(length, level)];

    /// <summary>
    /// Keeps a curve usable after its range changes: the first and last points move to the new ends (they never
    /// become middle points, or a growing range would leave a trail of them), middle points that no longer fit are
    /// dropped, and an empty curve becomes a flat line.
    /// </summary>
    public static List<CurvePoint> Fit(IEnumerable<CurvePoint> points, int length, double level)
    {
        var curve = points.ToList();
        if (curve.Count == 0) return Flat(length, level);

        var middle = curve.Skip(1).SkipLast(1).Where(p => p.Minutes > 0 && p.Minutes < length);
        return [new CurvePoint(0, curve[0].Level), .. middle, new CurvePoint(length, curve[^1].Level)];
    }

    /// <summary>
    /// A curve read at a moment: between two points the level runs straight from one to the other; before the first
    /// point and after the last it stays at theirs.
    /// </summary>
    public static double LevelAt(IReadOnlyList<CurvePoint> points, int minutes)
    {
        if (minutes <= points[0].Minutes) return points[0].Level;
        for (var i = 1; i < points.Count; i++)
        {
            if (minutes > points[i].Minutes) continue;
            var (from, to) = (points[i - 1], points[i]);
            var span = to.Minutes - from.Minutes;
            return span <= 0 ? to.Level : from.Level + (to.Level - from.Level) * (minutes - from.Minutes) / span;
        }
        return points[^1].Level;
    }

    /// <summary>
    /// Makes a saved curve span its whole range without changing its shape. Older versions could leave one that starts
    /// late or stops early: it reads flat beyond its points anyway, so a point at each end carrying that level on changes
    /// nothing. One that runs past the end is cut there, at the level it has there. Every point inside stays.
    /// </summary>
    public static List<CurvePoint> ReachEnds(IReadOnlyList<CurvePoint> points, int length)
    {
        if (points.Count == 0) return [];

        var curve = points.Where(p => p.Minutes >= 0 && p.Minutes <= length).ToList();
        if (curve.Count == 0 || curve[0].Minutes > 0) curve.Insert(0, new CurvePoint(0, LevelAt(points, 0)));
        if (curve[^1].Minutes < length) curve.Add(new CurvePoint(length, LevelAt(points, length)));
        return curve;
    }

    /// <summary>Adds a point where the user clicked, keeping the list in time order.</summary>
    public static List<CurvePoint> Add(IEnumerable<CurvePoint> points, double minutes, double level, int length)
    {
        var curve = points.ToList();
        var at = SnapTime(minutes, length);
        if (at <= 0 || at >= length || curve.Any(p => p.Minutes == at)) return curve;

        curve.Add(new CurvePoint(at, Math.Clamp(level, 0, 100)));
        return [.. curve.OrderBy(p => p.Minutes)];
    }

    /// <summary>Removes a point. The two ends of the curve stay put.</summary>
    public static List<CurvePoint> Remove(IEnumerable<CurvePoint> points, int index)
    {
        var curve = points.ToList();
        if (index <= 0 || index >= curve.Count - 1) return curve;

        curve.RemoveAt(index);
        return curve;
    }

    /// <summary>
    /// Moves a point. The first and last only move up and down; the others also stay between their
    /// neighbours, at least one step away from each.
    /// </summary>
    public static List<CurvePoint> Move(IEnumerable<CurvePoint> points, int index, double minutes, double level, int length)
    {
        var curve = points.ToList();
        if (index < 0 || index >= curve.Count) return curve;

        var isEnd = index == 0 || index == curve.Count - 1;
        var time = isEnd
            ? curve[index].Minutes
            : Math.Clamp(SnapTime(minutes, length), curve[index - 1].Minutes + StepMinutes, curve[index + 1].Minutes - StepMinutes);

        var neighbours = new List<double>();
        if (index > 0) neighbours.Add(curve[index - 1].Level);
        if (index < curve.Count - 1) neighbours.Add(curve[index + 1].Level);

        curve[index] = new CurvePoint(time, SnapLevel(level, neighbours));
        return curve;
    }
}
