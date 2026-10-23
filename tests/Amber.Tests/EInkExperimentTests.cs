#if DEBUG
using Amber.Tint;

namespace Amber.Tests;

// The e-ink experiment exists in test builds only, and must never reach the settings file: the installed (Release) Amber
// reads the same file and would throw all its settings away on a tint it doesn't know.
public class EInkExperimentTests
{
    [Fact]
    public void The_experiment_is_never_part_of_the_settings()
    {
        // Nothing in the settings can hold it: no e-ink tint, no property for the look or its amber amount.
        Assert.DoesNotContain(Enum.GetNames<TintMode>(), name => name.Contains("Ink", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(Settings).GetProperties(),
            p => p.PropertyType == typeof(EInkLook) || p.Name.Contains("Ink", StringComparison.OrdinalIgnoreCase));

        // Picking a look and an amber amount changes no setting.
        var settings = new Settings();
        var changes = new List<string?>();
        settings.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        try
        {
            EInkExperiment.Look = EInkLook.Soft;
            EInkExperiment.Amber = 60;
            EInkExperiment.Look = EInkLook.Soft;
            Assert.Empty(changes);
        }
        finally
        {
            EInkExperiment.Look = null;
            EInkExperiment.Amber = 0;
        }
    }

    [Fact]
    public void The_black_slider_runs_from_pure_black_to_the_charcoal_and_the_white_from_white_to_a_70_percent_grey()
    {
        Assert.Equal(0, EInkExperiment.InkGrey(0));
        Assert.Equal(0.15, EInkExperiment.InkGrey(100), 6);
        Assert.Equal(0.075, EInkExperiment.InkGrey(50), 6);
        Assert.Equal(1, EInkExperiment.PaperGrey(0));
        Assert.Equal(0.70, EInkExperiment.PaperGrey(100), 6);
        Assert.Equal(0.85, EInkExperiment.PaperGrey(50), 6);
    }

    [Fact]
    public void The_sliders_shape_the_look_and_classic_is_plain_greyscale()
    {
        try
        {
            EInkExperiment.Black = 100;
            EInkExperiment.White = 0;
            Assert.Equal(EInkLook.Soft, EInkExperiment.Shape(EInkLook.Soft));   // the defaults are the e-ink look itself

            EInkExperiment.Black = 40;
            EInkExperiment.White = 50;
            var shaped = EInkExperiment.Shape(EInkLook.Soft);
            Assert.Equal(0.06, shaped.Ink.R, 6);
            Assert.Equal(0.85, shaped.Paper.B, 6);

            EInkExperiment.Classic = true;
            var classic = EInkExperiment.Shape(EInkLook.Soft);
            Assert.Equal((0.0, 0.0, 0.0), classic.Ink);
            Assert.Equal((1.0, 1.0, 1.0), classic.Paper);
        }
        finally
        {
            EInkExperiment.Black = 100;
            EInkExperiment.White = 0;
            EInkExperiment.Classic = false;
        }
    }
}
#endif
