namespace Amber.Tests;

public class TileWaveTests
{
    const double Far = 400; // centre to the farthest corner

    [Fact]
    public void The_ring_starts_at_the_centre_and_reaches_the_far_corners_at_the_end()
    {
        Assert.Equal(0, TileWave.RingRadius(0, Far));
        Assert.Equal(Far, TileWave.RingRadius(TileWave.Duration, Far), 6);
        Assert.Equal(Far, TileWave.RingRadius(TileWave.Duration + 1, Far), 6);
    }

    [Fact]
    public void The_ring_slows_down_as_it_goes()
    {
        var firstHalf = TileWave.RingRadius(TileWave.Duration / 2, Far);
        Assert.True(firstHalf > Far / 2);
    }

    [Fact]
    public void A_tile_on_the_ring_is_at_the_peak()
    {
        var seconds = TileWave.Duration / 3;
        Assert.Equal(TileWave.PeakOpacity, TileWave.Opacity(TileWave.RingRadius(seconds, Far), seconds, Far), 6);
    }

    [Fact]
    public void Ahead_of_the_ring_tiles_brighten_over_half_the_band_and_are_dark_beyond()
    {
        var seconds = TileWave.Duration / 3;
        var ring = TileWave.RingRadius(seconds, Far);

        Assert.Equal(TileWave.PeakOpacity / 2, TileWave.Opacity(ring + TileWave.BandWidth / 4, seconds, Far), 6);
        Assert.Equal(0, TileWave.Opacity(ring + TileWave.BandWidth / 2, seconds, Far));
    }

    [Fact]
    public void Behind_the_ring_a_tile_fades_out_over_the_trail()
    {
        var distance = 200.0;
        var reached = TileWave.Duration * (1 - Math.Cbrt(1 - distance / Far));

        Assert.Equal(TileWave.PeakOpacity, TileWave.Opacity(distance, reached, Far), 6);
        Assert.Equal(TileWave.PeakOpacity / 2, TileWave.Opacity(distance, reached + TileWave.Trail / 2, Far), 6);
        Assert.Equal(0, TileWave.Opacity(distance, reached + TileWave.Trail, Far));
    }

    [Fact]
    public void Nothing_shows_before_the_wave_or_after_it()
    {
        Assert.Equal(0, TileWave.Opacity(100, -0.1, Far));
        Assert.Equal(0, TileWave.Opacity(Far, TileWave.TotalSeconds, Far));
        for (var d = 0.0; d <= Far; d += 10) Assert.Equal(0, TileWave.Opacity(d, TileWave.TotalSeconds + 0.01, Far));
    }
}
