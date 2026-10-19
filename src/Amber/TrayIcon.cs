using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Amber;

/// <summary>Notification-area icon. Double-click opens the window; right-click shows a native menu.</summary>
sealed class TrayIcon : IDisposable
{
    enum Command { Toggle = 1, Open, Quit, EInkFirst = 100 }

    const int CallbackMessage = 0x8001; // WM_APP + 1
    static readonly int TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    // Hidden window that receives the icon's mouse events.
    readonly HwndSource _window = new(new HwndSourceParameters("Amber tray") { WindowStyle = 0 });
    readonly IntPtr _iconOn = LoadIcon("amber.ico");
    readonly IntPtr _iconOff = LoadIcon("amber-off.ico");
    bool _isOn;

    public event Action? OpenRequested;
    public event Action? ToggleRequested;
    public event Action? QuitRequested;
#if DEBUG
    /// <summary>The e-ink experiment showing now, ticked in the menu (Debug builds only).</summary>
    public Amber.Tint.EInkLook? EInk { get; set; }
    /// <summary>An e-ink look was picked from the menu: show it, or stop showing it if it is the one showing.</summary>
    public event Action<Amber.Tint.EInkLook>? EInkRequested;
#endif

    public TrayIcon()
    {
        _window.AddHook(WndProc);
        AddToTray();
    }

    public void SetOn(bool isOn)
    {
        if (isOn == _isOn) return;
        _isOn = isOn;
        var data = CreateData();
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        var data = CreateData();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _window.Dispose();
        DestroyIcon(_iconOn);
        DestroyIcon(_iconOff);
    }

    void AddToTray()
    {
        var data = CreateData();
        Shell_NotifyIcon(NIM_ADD, ref data);
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    NOTIFYICONDATA CreateData() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
        uCallbackMessage = CallbackMessage,
        hIcon = _isOn ? _iconOn : _iconOff,
        szTip = _isOn ? "Amber is on" : "Amber is off",
    };

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            // With NOTIFYICON_VERSION_4 the low word of lParam is the mouse/keyboard event.
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case WM_LBUTTONDBLCLK: OpenRequested?.Invoke(); break;
                case WM_CONTEXTMENU: ShowMenu(); break;
            }
            handled = true;
        }
        else if (msg == TaskbarCreatedMessage)
        {
            AddToTray(); // Explorer restarted and dropped all tray icons.
        }
        return IntPtr.Zero;
    }

    void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, (nint)Command.Toggle, _isOn ? "Turn off" : "Turn on");
        AppendMenu(menu, MF_STRING, (nint)Command.Open, "Open Amber");
#if DEBUG
        // Test builds only: the e-ink experiments, to compare by eye in any app.
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        for (var k = 0; k < Amber.Tint.EInkLook.All.Length; k++)
        {
            var look = Amber.Tint.EInkLook.All[k];
            AppendMenu(menu, MF_STRING | (look == EInk ? MF_CHECKED : 0), (nint)Command.EInkFirst + k, look.Name + " (test)");
        }
#endif
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, (nint)Command.Quit, "Quit");
        SetMenuDefaultItem(menu, (uint)Command.Open, 0); // Bold, since double-click does the same.

        GetCursorPos(out var point);
        SetForegroundWindow(_window.Handle); // Without this the menu doesn't close when clicking elsewhere.
        var command = (Command)TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_NONOTIFY | TPM_RETURNCMD, point.X, point.Y, _window.Handle, IntPtr.Zero);
        DestroyMenu(menu);

        switch (command)
        {
            case Command.Toggle: ToggleRequested?.Invoke(); break;
            case Command.Open: OpenRequested?.Invoke(); break;
            case Command.Quit: QuitRequested?.Invoke(); break;
#if DEBUG
            case >= Command.EInkFirst when command - Command.EInkFirst < Amber.Tint.EInkLook.All.Length:
                EInkRequested?.Invoke(Amber.Tint.EInkLook.All[command - Command.EInkFirst]);
                break;
#endif
        }
    }

    static IntPtr LoadIcon(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        var size = GetSystemMetricsForDpi(SM_CXSMICON, GetDpiForSystem());
        var icon = LoadImage(IntPtr.Zero, path, IMAGE_ICON, size, size, LR_LOADFROMFILE);
        return icon != IntPtr.Zero ? icon : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    // Win32 interop

    const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4, NOTIFYICON_VERSION_4 = 4;
    const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    const int WM_CONTEXTMENU = 0x007B, WM_LBUTTONDBLCLK = 0x0203;
    const uint MF_STRING = 0x0, MF_CHECKED = 0x8, MF_SEPARATOR = 0x800;
    const uint TPM_RIGHTBUTTON = 0x2, TPM_NONOTIFY = 0x80, TPM_RETURNCMD = 0x100;
    const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10;
    const int SM_CXSMICON = 49;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, uint flags, nint id, string? text);
    [DllImport("user32.dll")] static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);
    [DllImport("user32.dll")] static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr options);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
}
