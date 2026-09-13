using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Amber.Tint;

/// <summary>Applies a color matrix to all displays through the Windows Magnification API.</summary>
public static class ScreenTint
{
    static bool _initialized;

    public static void Apply(float[] matrix)
    {
        if (!_initialized)
        {
            if (!MagInitialize()) throw new Win32Exception(Marshal.GetLastWin32Error());
            _initialized = true;
        }
        if (!MagSetFullscreenColorEffect(matrix)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    /// <summary>Restores normal colors. Safe to call any time, from any exit path; never throws.</summary>
    public static void Reset()
    {
        if (!_initialized) return;
        try
        {
            MagSetFullscreenColorEffect(TintMatrix.Identity);
            MagUninitialize();
        }
        catch
        {
            // Best effort: this runs during shutdown and crashes.
        }
        _initialized = false;
    }

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool MagUninitialize();

    // MAGCOLOREFFECT is 25 floats in a row, so a float[25] can be passed directly.
    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool MagSetFullscreenColorEffect(float[] effect);
}
