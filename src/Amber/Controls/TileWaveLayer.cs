using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Amber.Controls;

/// <summary>
/// The tiles of <see cref="TileWave"/>, over the window's background and under everything else. It draws nothing and
/// does no work at rest: <see cref="Play"/> runs one wave, redrawing on every frame until it has passed, then lets go.
/// It never takes the mouse.
/// </summary>
public class TileWaveLayer : FrameworkElement
{
    // Opacities are drawn in this many steps, one shared brush each.
    const int Steps = 50;

    static readonly Brush[] TileBrushes = Enumerable.Range(0, Steps + 1).Select(step =>
    {
        var brush = new SolidColorBrush(TileWave.TileColor) { Opacity = TileWave.PeakOpacity * step / Steps };
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();

    readonly Stopwatch _clock = new();
    bool _running;
    double _seconds;

    public TileWaveLayer() => IsHitTestVisible = false;

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
        if (_seconds >= TileWave.TotalSeconds) Stop();
        InvalidateVisual();
    }

    void Stop()
    {
        CompositionTarget.Rendering -= OnFrame;
        _clock.Reset();
        _running = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_running) return;

        double width = ActualWidth, height = ActualHeight;
        var center = new Point(width / 2, height / 2);
        var farthest = Math.Sqrt(center.X * center.X + center.Y * center.Y);
        const double stepX = TileWave.TileWidth + TileWave.Gap, stepY = TileWave.TileHeight + TileWave.Gap;

        for (var row = 0; row * stepY < height; row++)
        {
            var top = row * stepY;
            var shift = TileWave.Brick && row % 2 == 1 ? -stepX / 2 : 0;
            for (var left = shift; left < width; left += stepX)
            {
                var dx = left + TileWave.TileWidth / 2 - center.X;
                var dy = top + TileWave.TileHeight / 2 - center.Y;
                var opacity = TileWave.Opacity(Math.Sqrt(dx * dx + dy * dy), _seconds, farthest);
                var step = (int)Math.Round(opacity / TileWave.PeakOpacity * Steps);
                if (step <= 0) continue;
                dc.DrawRoundedRectangle(TileBrushes[Math.Min(step, Steps)], null, new Rect(left, top, TileWave.TileWidth, TileWave.TileHeight),
                    TileWave.Radius, TileWave.Radius);
            }
        }
    }
}
