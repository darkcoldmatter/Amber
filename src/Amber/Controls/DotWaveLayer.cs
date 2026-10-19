using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// The dots of <see cref="DotWave"/>, over the window's background and under everything else. It draws nothing and does
/// no work at rest: <see cref="Play"/> runs one wave, redrawing on every frame until it has passed, then lets go. The
/// screen of dots is laid out once for each window size. It never takes the mouse.
/// </summary>
public class DotWaveLayer : FrameworkElement
{
    // Opacities are drawn in this many steps, one shared brush each.
    const int Steps = 64;

    static readonly Brush[] DotBrushes = Enumerable.Range(0, Steps + 1).Select(step =>
    {
        var brush = new SolidColorBrush(DotWave.DotColor) { Opacity = DotWave.PeakOpacity * step / Steps };
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();

    /// <summary>A dot of the screen: where it sits from the centre, how far and in which direction, its size and strength.</summary>
    readonly record struct Dot(double X, double Y, double Distance, double DirectionX, double DirectionY, double Size, double Strength);

    readonly Stopwatch _clock = new();
    bool _running;
    double _seconds;
    Dot[] _dots = [];
    Size _laidOutFor;

    public DotWaveLayer() => IsHitTestVisible = false;

    protected override Size MeasureOverride(Size availableSize) => default;

    /// <summary>Starts a wave from the centre (again from the start if one is running). Skipped when Windows' animations are off.</summary>
    public void Play()
    {
        if (!SystemParameters.ClientAreaAnimation || !IsVisible) return;
        _clock.Restart();
        _seconds = 0;
        if (!_running) CompositionTarget.Rendering += OnFrame;
        _running = true;
        InvalidateVisual();
    }

    void OnFrame(object? sender, EventArgs e)
    {
        _seconds = _clock.Elapsed.TotalSeconds;
        if (_seconds >= DotWave.TotalSeconds) Stop();
        InvalidateVisual();
    }

    void Stop()
    {
        CompositionTarget.Rendering -= OnFrame;
        _clock.Reset();
        _running = false;
        _dots = [];
        _laidOutFor = default;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_running) return;

        var size = RenderSize;
        if (size != _laidOutFor) (_dots, _laidOutFor) = (LayOut(size), size);
        double cx = size.Width / 2, cy = size.Height / 2, farthest = Math.Sqrt(cx * cx + cy * cy);

        foreach (var dot in _dots)
        {
            var swell = DotWave.Swell(dot.Distance, _seconds, farthest);
            if (swell <= 0) continue;
            var step = (int)Math.Round(swell * dot.Strength * Steps);
            if (step <= 0) continue;

            var warp = DotWave.Warp(dot.Distance, _seconds, farthest);
            var side = DotWave.DotSize * dot.Size;
            dc.DrawRectangle(DotBrushes[Math.Min(step, Steps)], null,
                new Rect(cx + dot.X + dot.DirectionX * warp - side / 2, cy + dot.Y + dot.DirectionY * warp - side / 2, side, side));
        }
    }

    /// <summary>The screen for a window this size: a square grid, or one turned 45° (<see cref="DotWave.Screen45"/>), at the same spacing.</summary>
    static Dot[] LayOut(Size size)
    {
        double stepX = DotWave.Screen45 ? DotWave.Pitch * Math.Sqrt(2) : DotWave.Pitch;
        double stepY = DotWave.Screen45 ? DotWave.Pitch / Math.Sqrt(2) : DotWave.Pitch;
        double cx = size.Width / 2, cy = size.Height / 2;
        var dots = new List<Dot>();
        for (var row = 0; row * stepY < size.Height + stepY; row++)
        {
            var shift = DotWave.Screen45 && row % 2 == 1 ? stepX / 2 : 0;
            for (var column = 0; column * stepX + shift < size.Width + stepX; column++)
            {
                double x = column * stepX + shift - cx, y = row * stepY - cy, distance = Math.Sqrt(x * x + y * y);
                var (dotSize, strength) = DotWave.Character(column, row);
                dots.Add(new Dot(x, y, distance, distance > 0 ? x / distance : 0, distance > 0 ? y / distance : 0, dotSize, strength));
            }
        }
        return [.. dots];
    }
}
