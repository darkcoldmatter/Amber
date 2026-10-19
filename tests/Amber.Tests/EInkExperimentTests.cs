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
            EInkExperiment.Look = EInkLook.Crisp;
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
}
#endif
