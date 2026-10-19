namespace Amber.Tests;

public class DotWaveTests
{
    const double Far = 400; // centre to the farthest corner

    static double Reached(double distance) => DotWave.Duration * (1 - Math.Pow(1 - distance / Far, 1 / DotWave.EasePower));

    [Fact]
    public void The_ring_starts_at_the_centre_and_reaches_the_far_corners_at_the_end()
    {
        Assert.Equal(0, DotWave.RingRadius(0, Far));
        Assert.Equal(Far, DotWave.RingRadius(DotWave.Duration, Far), 6);
        Assert.True(DotWave.RingRadius(DotWave.Duration / 2, Far) > Far / 2); // it slows down as it goes
    }

    [Fact]
    public void A_dot_on_the_ring_is_at_the_peak_and_ahead_brightens_over_half_the_band()
    {
        var seconds = DotWave.Duration / 3;
        var ring = DotWave.RingRadius(seconds, Far);

        Assert.Equal(DotWave.PeakOpacity, DotWave.Opacity(ring, seconds, Far), 6);
        Assert.Equal(DotWave.PeakOpacity / 2, DotWave.Opacity(ring + DotWave.BandWidth / 4, seconds, Far), 6);
        Assert.Equal(0, DotWave.Opacity(ring + DotWave.BandWidth / 2, seconds, Far));
    }

    [Fact]
    public void Behind_the_ring_a_dot_fades_out_over_the_trail()
    {
        var reached = Reached(200);

        Assert.Equal(DotWave.PeakOpacity / 2, DotWave.Opacity(200, reached + DotWave.Trail / 2, Far), 6);
        Assert.Equal(0, DotWave.Opacity(200, reached + DotWave.Trail, Far), 6);
    }

    [Fact]
    public void The_ripple_pushes_dots_ahead_of_the_ring_outward_and_those_behind_inward()
    {
        var seconds = DotWave.Duration / 3;
        var ring = DotWave.RingRadius(seconds, Far);
        var quarter = DotWave.WarpWavelength / 4;

        Assert.Equal(0, DotWave.Warp(ring, seconds, Far), 6);
        Assert.True(DotWave.Warp(ring + quarter / 2, seconds, Far) > 0);
        Assert.True(DotWave.Warp(ring - quarter, seconds, Far) < 0);
        Assert.True(Math.Abs(DotWave.Warp(ring - quarter, seconds, Far)) <= DotWave.WarpAmplitude);
    }

    [Fact]
    public void The_ripple_fades_beyond_the_band_and_dies_away_behind()
    {
        var seconds = DotWave.Duration / 3;
        var ring = DotWave.RingRadius(seconds, Far);

        Assert.Equal(0, DotWave.Warp(ring + DotWave.BandWidth / 2, seconds, Far));
        var near = Math.Abs(DotWave.Warp(ring - DotWave.WarpWavelength / 4, seconds, Far));
        var far = Math.Abs(DotWave.Warp(ring - DotWave.WarpWavelength * 5 / 4, seconds, Far));
        Assert.True(far < near / 4, $"{far} vs {near}"); // a wavelength further back: e^(-60/40) as much
    }

    [Fact]
    public void Nothing_shows_or_moves_before_the_wave_or_after_it()
    {
        foreach (var seconds in new[] { -0.1, DotWave.TotalSeconds, DotWave.TotalSeconds + 1 })
            for (var d = 0.0; d <= Far; d += 10)
            {
                Assert.Equal(0, DotWave.Opacity(d, seconds, Far));
                Assert.Equal(0, DotWave.Warp(d, seconds, Far));
            }
    }

    [Fact]
    public void Each_dot_keeps_the_same_size_and_strength_within_the_jitter()
    {
        var strengths = new List<double>();
        for (var column = 0; column < 40; column++)
            for (var row = 0; row < 40; row++)
            {
                var (size, strength) = DotWave.Character(column, row);
                Assert.Equal((size, strength), DotWave.Character(column, row));
                Assert.InRange(size, 1 - DotWave.SizeJitter, 1 + DotWave.SizeJitter);
                Assert.InRange(strength, DotWave.FaintestDot, 1);
                strengths.Add(strength);
            }
        Assert.True(strengths.Distinct().Count() > 1000); // their strengths really do differ
    }
}
