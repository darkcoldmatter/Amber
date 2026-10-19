using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Amber;

/// <summary>
/// Registers the global shortcuts with Windows (RegisterHotKey) on a hidden message-only window, so they work with
/// Amber's window closed. Remembers which combinations another app already holds, for Settings to point out.
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    readonly HwndSource _window = new(new HwndSourceParameters("Amber shortcuts") { WindowStyle = 0, ParentWindow = HWND_MESSAGE });
    IReadOnlyList<ShortcutBinding> _wanted = [];
    // Registered ids are 1 + the binding's index in _wanted.
    readonly List<int> _registered = [];

    public GlobalHotkeys() => _window.AddHook(WndProc);

    /// <summary>A registered shortcut was pressed (and, for level up/down, again for each repeat while held).</summary>
    public event Action<ShortcutBinding>? Pressed;

    /// <summary><see cref="Failed"/> changed.</summary>
    public event Action? Changed;

    /// <summary>The actions with a combination Windows refused, last time they were registered: another app holds it.</summary>
    public IReadOnlySet<ShortcutAction> Failed { get; private set; } = new HashSet<ShortcutAction>();

    /// <summary>
    /// While Settings listens for a new shortcut, every shortcut is let go, so the press reaches the window. What
    /// <see cref="Failed"/> says is kept meanwhile.
    /// </summary>
    public bool Suspended
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (value) UnregisterAll();
            else RegisterAll();
        }
    }

    /// <summary>Registers these combinations instead of the ones before (none, when shortcuts are off).</summary>
    public void Use(IEnumerable<ShortcutBinding> bindings)
    {
        _wanted = bindings.ToList();
        if (Suspended) return;
        UnregisterAll();
        RegisterAll();
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }

    void RegisterAll()
    {
        var failed = new HashSet<ShortcutAction>();
        for (var i = 0; i < _wanted.Count; i++)
        {
            var binding = _wanted[i];
            // Holding repeats level up and down only; everything else fires once per press.
            var repeats = binding.Action is ShortcutAction.LevelUp or ShortcutAction.LevelDown;
            var flags = Flags(binding.Keys.Modifiers) | (repeats ? 0 : MOD_NOREPEAT);
            if (RegisterHotKey(_window.Handle, i + 1, flags, (uint)KeyInterop.VirtualKeyFromKey(binding.Keys.Key))) _registered.Add(i + 1);
            else failed.Add(binding.Action);
        }
        if (failed.SetEquals(Failed)) return;
        Failed = failed;
        Changed?.Invoke();
    }

    void UnregisterAll()
    {
        foreach (var id in _registered) UnregisterHotKey(_window.Handle, id);
        _registered.Clear();
    }

    static uint Flags(ModifierKeys modifiers) =>
        (modifiers.HasFlag(ModifierKeys.Alt) ? MOD_ALT : 0) | (modifiers.HasFlag(ModifierKeys.Control) ? MOD_CONTROL : 0) |
        (modifiers.HasFlag(ModifierKeys.Shift) ? MOD_SHIFT : 0) | (modifiers.HasFlag(ModifierKeys.Windows) ? MOD_WIN : 0);

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;
        var index = (int)wParam - 1;
        if (index >= 0 && index < _wanted.Count && _registered.Contains(index + 1)) Pressed?.Invoke(_wanted[index]);
        handled = true;
        return IntPtr.Zero;
    }

    const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
}
