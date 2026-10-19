using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Amber.Tint;

namespace Amber;

/// <summary>A point on a filter's curve: minutes from the start of its range, and a level of 0–100.</summary>
public sealed record CurvePoint(int Minutes, double Level);

/// <summary>One advanced-mode filter: a mode over a time range, at a flat level or along a curve.</summary>
public sealed class Filter : INotifyPropertyChanged
{
    public TintMode Mode { get; set => Set(ref field, value); }

    /// <summary>This filter's own colour while its tint is Custom, as a hue of 0–360 (red by default).</summary>
    public double Hue { get; set => Set(ref field, value); }

    public TimeOnly Start
    {
        get;
        set { if (Set(ref field, value)) FitCurve(); }
    }

    public TimeOnly End
    {
        get;
        set { if (Set(ref field, value)) FitCurve(); }
    } = new(1, 0);
    public double Level { get; set => Set(ref field, value); } = 50;

    /// <summary>The curve replaces the flat level while this is on; turning it off keeps the curve for later.</summary>
    public bool CurveEnabled { get; set => Set(ref field, value); }

    public List<CurvePoint> Curve { get; set => Set(ref field, value); } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public int LengthMinutes => ScheduleRange.Length(ScheduleRange.Minutes(Start), ScheduleRange.Minutes(End));

    /// <summary>Start and end as minutes since midnight, the way the timeline and the layout rules work with them.</summary>
    [JsonIgnore]
    public (int Start, int End) Range => (ScheduleRange.Minutes(Start), ScheduleRange.Minutes(End));

    /// <summary>The level at a point in the range: the flat level, or the curve read between its points.</summary>
    public double LevelAt(double minutesFromStart) =>
        CurveEnabled && Curve.Count > 0 ? CurveMath.LevelAt(Curve, minutesFromStart) : Level;

    /// <summary>A fresh curve is just a flat line at the current level, from one end of the range to the other.</summary>
    public void ResetCurve() => Curve = CurveMath.Flat(LengthMinutes, Level);

    /// <summary>After the range moves, the curve keeps its ends and drops anything that no longer fits.</summary>
    void FitCurve()
    {
        if (Curve.Count > 0) Curve = CurveMath.Fit(Curve, LengthMinutes, Level);
    }

    bool Set<T>(ref T storage, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
