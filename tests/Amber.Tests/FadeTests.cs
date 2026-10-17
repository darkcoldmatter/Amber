namespace Amber.Tests;

public class FadeTests
{
    const double FadeSeconds = 30;

    [Fact]
    public void A_full_fade_in_takes_the_whole_fade_time()
    {
        var fade = 0.0;
        for (var second = 0; second < 30; second++) fade = Fade.Step(fade, wanted: true, 1, FadeSeconds);
        Assert.Equal(1, fade, 3);
    }

    [Fact]
    public void Halfway_through_the_fade_it_is_half_on()
    {
        Assert.Equal(0.5, Fade.Step(0, wanted: true, 15, FadeSeconds), 3);
    }

    [Fact]
    public void Switching_off_fades_back_down()
    {
        Assert.Equal(0.5, Fade.Step(1, wanted: false, 15, FadeSeconds), 3);
        Assert.Equal(0, Fade.Step(0.2, wanted: false, 15, FadeSeconds), 3);
    }

    [Fact]
    public void It_never_goes_past_the_ends()
    {
        Assert.Equal(1, Fade.Step(0.9, wanted: true, 60, FadeSeconds));
        Assert.Equal(0, Fade.Step(0.1, wanted: false, 60, FadeSeconds));
    }

    [Fact]
    public void A_fade_steps_about_1_percent_at_a_time_but_between_100_and_500_ms()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(100), Fade.Tick(AdvancedSchedule.FadeSeconds));   // 10 s between filters
        Assert.Equal(TimeSpan.FromMilliseconds(300), Fade.Tick(30));
        Assert.Equal(TimeSpan.FromMilliseconds(500), Fade.Tick(30 * 60));   // a long fade still steps twice a second
        Assert.Equal(TimeSpan.FromMilliseconds(100), Fade.Tick(2));         // never faster than 100 ms
    }
}
