using Microsoft.Win32;

namespace Amber;

/// <summary>Starts Amber in the tray when the user signs in to Windows.</summary>
public static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Amber";

    /// <summary>
    /// Whether Amber, as it starts, points an existing sign-in entry at its own exe again (in case it was moved).
    /// Only an installed copy does: a build run from source (dotnet run) must not take over the installed Amber's
    /// entry. The Settings switch still writes the entry whenever it is clicked.
    /// </summary>
    public static bool ShouldRefresh(bool installed, bool enabled) => installed && enabled;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {App.TrayArgument}");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
