using Amber.Tint;

namespace Amber.Tests;

public class TintPlanTests
{
    static readonly float[] Target = TintMatrix.Build(TintMode.GoldenHour, 80);

    [Fact]
    public void Advanced_mode_eases_the_tint_in_over_30_seconds()
    {
        var plan = new TintPlan();

        var afterASecond = plan.Next(Target, eased: true, 1);
        Assert.NotNull(afterASecond);
        Assert.True(plan.Fade < 0.1, "the tint should only just be starting");
        Assert.True(afterASecond![12] > Target[12], "barely any tint yet");

        plan.Next(Target, eased: true, 14);
        Assert.Equal(0.5, plan.Fade, 2);

        var full = plan.Next(Target, eased: true, 15);
        Assert.Equal(1, plan.Fade, 3);
        Assert.Equal(Target, full);
    }

    [Fact]
    public void Switching_advanced_off_eases_back_to_a_clear_screen()
    {
        var plan = new TintPlan();
        plan.Next(Target, eased: true, 30);

        plan.Next(null, eased: true, 15);
        Assert.Equal(0.5, plan.Fade, 2);
        Assert.True(plan.IsFading);

        Assert.Null(plan.Next(null, eased: true, 15));
        Assert.Equal(0, plan.Fade);
        Assert.False(plan.IsFading);
    }

    [Fact]
    public void Simple_mode_and_previews_are_shown_as_they_are()
    {
        var plan = new TintPlan();

        Assert.Equal(Target, plan.Next(Target, eased: false, 0.1));
        Assert.Equal(1, plan.Fade);
        Assert.Null(plan.Next(null, eased: false, 0.1));
    }
}
