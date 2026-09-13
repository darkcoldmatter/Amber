using Microsoft.Win32;

namespace Amber;

/// <summary>Starts Amber in the tray when the user signs in to Windows.</summary>
static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Amber";

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
