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

    static readonly Brush[] ShadowBrushes = Enumerable.Range(0, Steps + 1).Select(step =>
    {
        var brush = new SolidColorBrush(Colors.Black) { Opacity = TileWave.ShadowOpacity * step / Steps };
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();

    readonly List<(Rect Tile, int Step, double Swell)> _lit = [];
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
        _lit.Clear();
        _lit.TrimExcess();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!_running) return;

        double width = ActualWidth, height = ActualHeight;
        var center = new Point(width / 2, height / 2);
        var farthest = Math.Sqrt(center.X * center.X + center.Y * center.Y);
        const double stepX = TileWave.TileWidth + TileWave.Gap, stepY = TileWave.TileHeight + TileWave.Gap;

        // The lit tiles, each raised and pushed along its line from the centre; shadows all go under all the tiles.
        _lit.Clear();
        for (var row = 0; row * stepY < height; row++)
        {
            var top = row * stepY;
            var shift = TileWave.Brick && row % 2 == 1 ? -stepX / 2 : 0;
            for (var left = shift; left < width; left += stepX)
            {
                var dx = left + TileWave.TileWidth / 2 - center.X;
                var dy = top + TileWave.TileHeight / 2 - center.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var swell = TileWave.Swell(distance, _seconds, farthest);
                var step = (int)Math.Round(swell * Steps);
                if (step <= 0) continue;

                var push = distance > 0 ? TileWave.Offset(distance, _seconds, farthest) / distance : 0;
                double scale = TileWave.Scale(distance, _seconds, farthest), w = TileWave.TileWidth * scale, h = TileWave.TileHeight * scale;
                var tile = new Rect(center.X + dx * (1 + push) - w / 2, center.Y + dy * (1 + push) - h / 2, w, h);
                _lit.Add((tile, Math.Min(step, Steps), swell));
            }
        }

        if (TileWave.ShadowOpacity > 0)
            foreach (var (tile, step, swell) in _lit)
            {
                var shadow = tile;
                shadow.Offset(0, TileWave.ShadowDrop * swell);
                Draw(dc, ShadowBrushes[step], shadow);
            }
        foreach (var (tile, step, _) in _lit) Draw(dc, TileBrushes[step], tile);
    }

    static void Draw(DrawingContext dc, Brush brush, Rect tile) => dc.DrawRoundedRectangle(brush, null, tile, TileWave.Radius, TileWave.Radius);
}
