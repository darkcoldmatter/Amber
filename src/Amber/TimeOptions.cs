namespace Amber;

/// <summary>Choices for the schedule dropdowns.</summary>
public static class TimeOptions
{
    static readonly int[] TransitionChoices = [0, 10, 30, 60, 120, 300, 600, 900, 1800];

    // The hours depend on the clock Windows uses: TimeFormat.Hours.
    public static IReadOnlyList<string> Minutes { get; } =
        Enumerable.Range(0, 60 / ScheduleStep).Select(step => (step * ScheduleStep).ToString("00")).ToArray();

    public const int ScheduleStep = 15;

    /// <summary>
    /// Transition lengths in seconds for a schedule of the given length. 0 is "Off";
    /// longer fades stop at 30 minutes and never take more than half the scheduled time.
    /// </summary>
    public static IReadOnlyList<int> Transitions(int scheduleMinutes)
    {
        var longest = scheduleMinutes * 60 / 2;
        return TransitionChoices.Where(seconds => seconds <= longest).ToArray();
    }

    /// <summary>How a transition length reads in the dropdown: "Off", "10 s", "1 min".</summary>
    public static string TransitionLabel(int seconds) =>
        seconds == 0 ? "Off" : seconds < 60 ? $"{seconds} s" : $"{seconds / 60} min";
}
