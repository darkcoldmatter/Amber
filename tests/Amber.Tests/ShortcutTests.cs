using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Amber.Tint;
using Amber.Views;

namespace Amber.Tests;

public class ShortcutTests
{
    const ModifierKeys CtrlAlt = ModifierKeys.Control | ModifierKeys.Alt;

    [Theory]
    [InlineData("Ctrl+Alt+A", CtrlAlt, Key.A)]
    [InlineData("Ctrl+Alt+Shift+1", CtrlAlt | ModifierKeys.Shift, Key.D1)]
    [InlineData("Ctrl+Alt+=", CtrlAlt, Key.OemPlus)]
    [InlineData("Ctrl+Alt+-", CtrlAlt, Key.OemMinus)]
    [InlineData("Win+0", ModifierKeys.Windows, Key.D0)]
    [InlineData("Alt+F4", ModifierKeys.Alt, Key.F4)]
    [InlineData("Ctrl+F12", ModifierKeys.Control, Key.F12)]
    [InlineData("Shift+Ctrl+z", ModifierKeys.Control | ModifierKeys.Shift, Key.Z)]
    [InlineData(" ctrl + alt + a ", CtrlAlt, Key.A)]
    public void Text_parses_into_modifiers_and_a_key(string text, ModifierKeys modifiers, Key key)
    {
        Assert.Equal(new Shortcut(modifiers, key), Shortcut.Parse(text));
    }

    [Theory]
    [InlineData("Ctrl+Alt+A")]
    [InlineData("Ctrl+Alt+Shift+Win+9")]
    [InlineData("Ctrl+Alt+=")]
    [InlineData("Ctrl+Alt+-")]
    [InlineData("Win+F10")]
    public void Saved_text_round_trips(string text)
    {
        Assert.Equal(text, Shortcut.Parse(text)!.ToString());
    }

    [Fact]
    public void Modifiers_are_written_in_one_order_and_the_pill_spaces_them_out()
    {
        var shortcut = Shortcut.Parse("Win+Shift+Alt+Ctrl+=")!;

        Assert.Equal("Ctrl+Alt+Shift+Win+=", shortcut.ToString());
        Assert.Equal("Ctrl + Alt + Shift + Win + =", shortcut.Display);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]                // a lone key would take over typing
    [InlineData("Shift+A")]          // so would Shift + a key
    [InlineData("Shift+1")]
    [InlineData("Ctrl+Alt")]         // no key
    [InlineData("Ctrl+Alt+")]
    [InlineData("Ctrl+Ctrl+A")]      // a modifier twice
    [InlineData("Ctrl+Alt+AB")]
    [InlineData("Ctrl+Alt+Space")]   // not a key Amber offers
    [InlineData("Ctrl+Alt+F13")]
    [InlineData("Ctrl+Alt+F0")]
    [InlineData("Ctrl+Alt+F01")]
    [InlineData("Hyper+A")]
    [InlineData("Ctrl+Alt+A+B")]
    public void Invalid_text_is_no_shortcut(string? text)
    {
        Assert.Null(Shortcut.Parse(text));
    }

    [Theory]
    [InlineData(ModifierKeys.Control, ShortcutProblem.None)]
    [InlineData(ModifierKeys.Alt, ShortcutProblem.None)]
    [InlineData(ModifierKeys.Windows, ShortcutProblem.None)]
    [InlineData(ModifierKeys.Shift | ModifierKeys.Alt, ShortcutProblem.None)]
    [InlineData(ModifierKeys.None, ShortcutProblem.NeedsModifier)]
    [InlineData(ModifierKeys.Shift, ShortcutProblem.NeedsModifier)]
    public void A_shortcut_needs_ctrl_alt_or_win(ModifierKeys modifiers, ShortcutProblem problem)
    {
        Assert.Equal(problem, Shortcut.Check(modifiers, Key.A));
    }

    [Fact]
    public void Only_letters_digits_equals_minus_and_F1_to_F12_are_offered()
    {
        Assert.Equal(ShortcutProblem.None, Shortcut.Check(CtrlAlt, Key.Q));
        Assert.Equal(ShortcutProblem.None, Shortcut.Check(CtrlAlt, Key.D7));
        Assert.Equal(ShortcutProblem.None, Shortcut.Check(CtrlAlt, Key.F1));
        Assert.Equal(ShortcutProblem.KeyNotAllowed, Shortcut.Check(CtrlAlt, Key.F13));
        Assert.Equal(ShortcutProblem.KeyNotAllowed, Shortcut.Check(CtrlAlt, Key.Space));
        Assert.Equal(ShortcutProblem.KeyNotAllowed, Shortcut.Check(CtrlAlt, Key.NumPad1));
        Assert.Equal(ShortcutProblem.KeyNotAllowed, Shortcut.Check(CtrlAlt, Key.Tab));
    }

    // Defaults and bindings

    [Fact]
    public void Defaults_are_ctrl_alt_and_on()
    {
        var shortcuts = new ShortcutSettings();

        Assert.True(shortcuts.Enabled);
        Assert.Equal("Ctrl+Alt+A", shortcuts.Toggle!.ToString());
        Assert.Equal("Ctrl+Alt+=", shortcuts.LevelUp!.ToString());
        Assert.Equal("Ctrl+Alt+-", shortcuts.LevelDown!.ToString());
        Assert.Equal("Ctrl+Alt+1", shortcuts.GoldenHour!.ToString());
        Assert.Equal("Ctrl+Alt+2", shortcuts.Amber!.ToString());
        Assert.Equal("Ctrl+Alt+3", shortcuts.DeepNight!.ToString());
        Assert.Equal("Ctrl+Alt+4", shortcuts.Custom!.ToString());
    }

    [Fact]
    public void Defaults_make_7_different_combinations()
    {
        var bindings = new ShortcutSettings().Bindings().ToList();

        Assert.Equal(Enum.GetValues<ShortcutAction>(), bindings.Select(b => b.Action));
        Assert.Equal(7, bindings.Select(b => b.Keys).Distinct().Count());
    }

    [Fact]
    public void A_shortcut_set_to_none_is_left_out()
    {
        var shortcuts = new ShortcutSettings().With(ShortcutAction.Toggle, null).With(ShortcutAction.DeepNight, null);

        Assert.Null(shortcuts.Toggle);
        Assert.Null(shortcuts.DeepNight);
        Assert.DoesNotContain(shortcuts.Bindings(), b => b.Action is ShortcutAction.Toggle or ShortcutAction.DeepNight);
        Assert.Equal(5, shortcuts.Bindings().Count());
    }

    [Fact]
    public void Changing_a_shortcut_makes_a_different_value_and_keeps_the_others()
    {
        var before = new ShortcutSettings();
        var after = before.With(ShortcutAction.Amber, Shortcut.Parse("Ctrl+Alt+M"));

        Assert.NotEqual(before, after);
        Assert.Equal("Ctrl+Alt+M", after.Amber!.ToString());
        Assert.Equal(before.Toggle, after.Toggle);
        Assert.Equal(new ShortcutSettings(), new ShortcutSettings()); // equal values: no needless re-registering
    }

    // Duplicates within Amber

    [Fact]
    public void Keys_another_action_uses_are_reported()
    {
        var shortcuts = new ShortcutSettings();

        Assert.Equal(ShortcutAction.Toggle, shortcuts.UsedBy(ShortcutAction.Amber, Shortcut.Parse("Ctrl+Alt+A")!));
        Assert.Equal(ShortcutAction.DeepNight, shortcuts.UsedBy(ShortcutAction.Toggle, Shortcut.Parse("Ctrl+Alt+3")!));
        Assert.Null(shortcuts.UsedBy(ShortcutAction.Toggle, Shortcut.Parse("Ctrl+Alt+T")!));
        Assert.Null(shortcuts.UsedBy(ShortcutAction.Toggle, Shortcut.Parse("Ctrl+Alt+Shift+3")!)); // Shift makes it different
    }

    [Fact]
    public void An_action_doesnt_clash_with_itself()
    {
        var shortcuts = new ShortcutSettings();

        Assert.Null(shortcuts.UsedBy(ShortcutAction.Toggle, Shortcut.Parse("Ctrl+Alt+A")!));
        Assert.Null(shortcuts.UsedBy(ShortcutAction.Amber, Shortcut.Parse("Ctrl+Alt+2")!));
    }

    [Theory]
    [InlineData(ShortcutAction.Toggle, "Turn on / off")]
    [InlineData(ShortcutAction.LevelUp, "Level up")]
    [InlineData(ShortcutAction.DeepNight, "Deep night")]
    public void Actions_have_their_settings_words(ShortcutAction action, string label)
    {
        Assert.Equal(label, ShortcutSettings.Label(action));
    }

    // What a press does

    [Fact]
    public void On_off_acts_on_the_current_mode_like_the_tray()
    {
        var simple = new Settings();
        ShortcutCommands.Run(ShortcutAction.Toggle, simple);
        Assert.True(simple.IsOn);
        ShortcutCommands.Run(ShortcutAction.Toggle, simple);
        Assert.False(simple.IsOn);

        var advanced = new Settings { IsAdvanced = true };
        ShortcutCommands.Run(ShortcutAction.Toggle, advanced);
        Assert.True(advanced.AdvancedIsOn);
        Assert.False(advanced.IsOn);
    }

    [Fact]
    public void Level_steps_two_percent_and_stays_within_0_to_100()
    {
        var settings = new Settings { Level = 50 };
        ShortcutCommands.Run(ShortcutAction.LevelUp, settings);
        Assert.Equal(52, settings.Level);
        ShortcutCommands.Run(ShortcutAction.LevelDown, settings);
        ShortcutCommands.Run(ShortcutAction.LevelDown, settings);
        Assert.Equal(48, settings.Level);

        Assert.Equal(100, ShortcutCommands.Step(100, +2));
        Assert.Equal(100, ShortcutCommands.Step(99, +2));   // only the ends clip
        Assert.Equal(0, ShortcutCommands.Step(1, -2));
        Assert.Equal(0, ShortcutCommands.Step(0, -2));
        Assert.Equal(52, ShortcutCommands.Step(50.4, +2));  // whole percents: rounded first
        Assert.Equal(49, ShortcutCommands.Step(50.5, -2));
    }

    [Fact]
    public void An_odd_level_keeps_stepping_by_two()
    {
        var settings = new Settings { Level = 37 };
        ShortcutCommands.Run(ShortcutAction.LevelUp, settings);
        Assert.Equal(39, settings.Level);
        ShortcutCommands.Run(ShortcutAction.LevelDown, settings);
        ShortcutCommands.Run(ShortcutAction.LevelDown, settings);
        Assert.Equal(35, settings.Level);
    }

    [Theory]
    [InlineData(ShortcutAction.GoldenHour, TintMode.GoldenHour)]
    [InlineData(ShortcutAction.Amber, TintMode.Amber)]
    [InlineData(ShortcutAction.DeepNight, TintMode.DeepNight)]
    [InlineData(ShortcutAction.Custom, TintMode.Custom)]
    public void A_tint_shortcut_picks_that_tint(ShortcutAction action, TintMode mode)
    {
        var settings = new Settings { Mode = action == ShortcutAction.Amber ? TintMode.DeepNight : TintMode.Amber };
        ShortcutCommands.Run(action, settings);
        Assert.Equal(mode, settings.Mode);
    }

    [Fact]
    public void Level_and_tint_shortcuts_never_switch_the_tint_on()
    {
        var settings = new Settings { IsOn = false, Level = 20 };
        ShortcutCommands.Run(ShortcutAction.LevelUp, settings);
        ShortcutCommands.Run(ShortcutAction.DeepNight, settings);

        Assert.False(settings.IsOn);
        Assert.False(settings.AdvancedIsOn);
        Assert.Equal(22, settings.Level);
        Assert.Equal(TintMode.DeepNight, settings.Mode);
    }

    [Fact]
    public void Level_and_tint_shortcuts_do_nothing_in_advanced()
    {
        var settings = new Settings { IsAdvanced = true, Level = 20, Mode = TintMode.Amber };
        var changes = new List<string?>();
        settings.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        foreach (var action in new[] { ShortcutAction.LevelUp, ShortcutAction.LevelDown,
                     ShortcutAction.GoldenHour, ShortcutAction.DeepNight, ShortcutAction.Custom })
            ShortcutCommands.Run(action, settings);

        Assert.Empty(changes);
    }

    // Capturing a new shortcut in Settings

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.LWin)]
    public void Lone_modifiers_wait_for_the_rest(Key key)
    {
        Assert.Equal(CaptureKind.Wait, ShortcutCapture.Read(key, ModifierKeys.Control).Kind);
    }

    [Fact]
    public void Esc_cancels_and_backspace_or_delete_clear()
    {
        Assert.Equal(CaptureKind.Cancel, ShortcutCapture.Read(Key.Escape, ModifierKeys.None).Kind);
        Assert.Equal(CaptureKind.Clear, ShortcutCapture.Read(Key.Back, ModifierKeys.None).Kind);
        Assert.Equal(CaptureKind.Clear, ShortcutCapture.Read(Key.Delete, ModifierKeys.None).Kind);
    }

    [Fact]
    public void A_usable_press_is_the_new_shortcut()
    {
        var result = ShortcutCapture.Read(Key.T, CtrlAlt);

        Assert.Equal(CaptureKind.Done, result.Kind);
        Assert.Equal(new Shortcut(CtrlAlt, Key.T), result.Keys);
    }

    [Fact]
    public void Unusable_presses_are_refused_with_a_reason()
    {
        Assert.Equal(CaptureProblem.NeedsModifier, ShortcutCapture.Read(Key.T, ModifierKeys.None).Problem);
        Assert.Equal(CaptureProblem.NeedsModifier, ShortcutCapture.Read(Key.T, ModifierKeys.Shift).Problem);
        Assert.Equal(CaptureProblem.KeyNotAllowed, ShortcutCapture.Read(Key.Space, CtrlAlt).Problem);
        Assert.Equal(CaptureProblem.KeyNotAllowed, ShortcutCapture.Read(Key.Escape, CtrlAlt).Problem);
        Assert.Equal(CaptureKind.Refused, ShortcutCapture.Read(Key.Space, CtrlAlt).Kind);
    }

    // What Settings shows

    [Fact]
    public void Pills_show_the_keys_spaced_out_or_not_set()
    {
        var shortcuts = new ShortcutSettings().With(ShortcutAction.Custom, null);

        Assert.Equal("Ctrl + Alt + A", SettingsView.PillText(shortcuts, ShortcutAction.Toggle));
        Assert.Equal("Ctrl + Alt + =", SettingsView.PillText(shortcuts, ShortcutAction.LevelUp));
        Assert.Equal("Ctrl + Alt + 1", SettingsView.PillText(shortcuts, ShortcutAction.GoldenHour));
        Assert.Equal("Not set", SettingsView.PillText(shortcuts, ShortcutAction.Custom));
    }

    [Fact]
    public void A_row_notes_a_refusal_first_then_another_app_holding_it()
    {
        Assert.Equal("Add Ctrl, Alt or Win", SettingsView.Note(ShortcutCapture.Message(CaptureProblem.NeedsModifier), true, true));
        Assert.Equal("In use by another app", SettingsView.Note(null, enabled: true, failed: true));
        Assert.Null(SettingsView.Note(null, enabled: false, failed: true)); // switched off: nothing is registered
        Assert.Null(SettingsView.Note(null, enabled: true, failed: false));
        Assert.Equal("Already used for Turn on / off", SettingsView.AlreadyUsed(ShortcutAction.Toggle));
    }

    // Registering with Windows: an unusual combination on throwaway windows, registered and let go again. No key is pressed.

    [Fact]
    public void Registration_reports_a_combination_already_held()
    {
        RunOnSta(() =>
        {
            var rare = new ShortcutBinding(ShortcutAction.Custom, Shortcut.Parse("Ctrl+Alt+Shift+F12")!);
            using var first = new GlobalHotkeys();
            using var second = new GlobalHotkeys();
            var changes = 0;
            second.Changed += () => changes++;

            first.Use([rare]);
            if (first.Failed.Count > 0) return; // something on this PC really holds it: nothing to learn here
            second.Use([rare]);

            Assert.Equal([ShortcutAction.Custom], second.Failed);
            Assert.Equal(1, changes);

            // Suspending the first lets it go, and the second can take it; suspending keeps what Failed said.
            first.Suspended = true;
            second.Use([rare]);
            Assert.Empty(second.Failed);
            first.Suspended = false;
            Assert.Equal([ShortcutAction.Custom], first.Failed);

            first.Use([]);
            Assert.Empty(first.Failed);
        });
    }

    [Fact]
    public void A_hotkey_message_runs_its_binding()
    {
        RunOnSta(() =>
        {
            var rare = new ShortcutBinding(ShortcutAction.LevelUp, Shortcut.Parse("Ctrl+Alt+Shift+F11")!);
            using var hotkeys = new GlobalHotkeys();
            var pressed = new List<ShortcutBinding>();
            hotkeys.Pressed += pressed.Add;
            hotkeys.Use([rare]);
            if (hotkeys.Failed.Count > 0) return;

            // The message Windows would post for it, sent straight to this test's own hidden window (no key press, and
            // never a window found by name: that could be a running Amber's).
            var source = (HwndSource)typeof(GlobalHotkeys).GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hotkeys)!;
            var window = source.Handle;
            Assert.NotEqual(IntPtr.Zero, window);
            SendMessage(window, 0x0312, 1, IntPtr.Zero);
            SendMessage(window, 0x0312, 2, IntPtr.Zero); // not registered: ignored

            Assert.Equal([rare], pressed);
        });
    }

    static void RunOnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Exception("Failed on the STA thread", failure);
    }

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, int message, nint wParam, IntPtr lParam);
}
