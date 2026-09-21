using Amber.Tint;

namespace Amber.Tests;

public class TintPlanTests
{
    static readonly float[] Target = TintMatrix.Build(TintMode.GoldenHour, 80);

    [Fact]
    public void The_tint_eases_in_over_the_fade_time()
    {
        var plan = new TintPlan();

        var justStarted = plan.Next(Target, eased: true, 0.1, fadeSeconds: 2);
        Assert.NotNull(justStarted);
        Assert.True(plan.Fade < 0.1, "the tint should only just be starting");
        Assert.True(justStarted![12] > Target[12], "barely any tint yet");

        plan.Next(Target, eased: true, 0.9, fadeSeconds: 2);
        Assert.Equal(0.5, plan.Fade, 2);

        var full = plan.Next(Target, eased: true, 1, fadeSeconds: 2);
        Assert.Equal(1, plan.Fade, 3);
        Assert.Equal(Target, full);
    }

    [Fact]
    public void Switching_off_eases_back_to_a_clear_screen()
    {
        var plan = new TintPlan();
        plan.Next(Target, eased: true, 2, fadeSeconds: 2);

        plan.Next(null, eased: true, 1, fadeSeconds: 2);
        Assert.Equal(0.5, plan.Fade, 2);
        Assert.True(plan.IsFading);

        Assert.Null(plan.Next(null, eased: true, 1, fadeSeconds: 2));
        Assert.Equal(0, plan.Fade);
        Assert.False(plan.IsFading);
    }

    [Fact]
    public void A_preview_is_shown_as_it_is()
    {
        var plan = new TintPlan();

        Assert.Equal(Target, plan.Next(Target, eased: false, 0.1));
        Assert.Equal(1, plan.Fade);
        Assert.Null(plan.Next(null, eased: false, 0.1));
    }

    [Fact]
    public void Releasing_a_preview_leaves_no_tint_behind()
    {
        var plan = new TintPlan();
        plan.Next(Target, eased: false, 0.1);   // dragging the level

        plan.SnapTo(null);                       // released, and nothing should be showing

        Assert.Equal(0, plan.Fade);
        Assert.False(plan.IsFading);
        Assert.Null(plan.Next(null, eased: true, 0.1));
    }
}
