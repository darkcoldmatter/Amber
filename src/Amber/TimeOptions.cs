namespace Amber;

/// <summary>Choices for the schedule dropdowns.</summary>
public static class TimeOptions
{
    static readonly int[] TransitionChoices = [0, 1, 2, 3, 5, 10, 15, 20, 30];

    public static IReadOnlyList<string> Hours { get; } =
        Enumerable.Range(0, 24).Select(hour => hour.ToString("00")).ToArray();

    public static IReadOnlyList<string> Minutes { get; } =
        Enumerable.Range(0, 60 / ScheduleStep).Select(step => (step * ScheduleStep).ToString("00")).ToArray();

    public const int ScheduleStep = 15;

    /// <summary>
    /// Transition lengths in minutes for a schedule of the given length. 0 means a 10-second fade;
    /// longer fades stop at 30 minutes and never take more than half the scheduled time.
    /// </summary>
    public static IReadOnlyList<int> Transitions(int scheduleMinutes)
    {
        var longest = Math.Min(30, scheduleMinutes / 2);
        return TransitionChoices.Where(minutes => minutes <= longest).ToArray();
    }
}
