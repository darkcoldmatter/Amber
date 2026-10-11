# Amber — project notes

Amber is a lightweight Windows 10/11 app that reduces blue light by tinting the whole screen.
C# / WPF on .NET 10, x64. It lives in the tray; the window is optional.

**Owner:** Dmitriy (designer, not a developer). Explain things plainly, keep changes small, and don't
start the next step until he has checked the current one. He tests every step by hand on his own PC.

**Status:** the whole experience works end to end — simple mode, the schedule engine, advanced mode with
the curve editor. What's left is a UI polish pass against a redesign, then installer and release.

---

## The tint engine (the part to get exactly right)

- **How the screen is tinted:** `MagSetFullscreenColorEffect` from `Magnification.dll` (P/Invoke in
  `Tint/ScreenTint.cs`). No admin rights, no overlay window, works on Windows 8+. `MagInitialize` first,
  `MagUninitialize` on exit, and **always reset to the identity matrix on every exit path**.
  Tested: Windows clears the tint by itself if the process is killed; the tint survives lock and sleep;
  the mouse cursor is not tinted; screenshots don't capture it.
- **The matrix** is 25 floats, row-major, input treated as the row vector `[R G B A 1]`; element `[i, j]`
  is how much input channel `i` adds to output channel `j` (`Tint/TintMatrix.cs`).
- **How a look is built:** each mode has a *weak* look (level 0) and a *strong* look (level 100), and the
  level interpolates between them. A look is a target color plus a "mono" amount:
  `output[j] = color[j] * ((1 - mono) * input[j] + mono * luma)`, with Rec.709 luma weights
  (0.2126, 0.7152, 0.0722) applied to gamma-encoded sRGB — approximate on purpose, tuned by eye.
  White always maps to the target color, so highlights can't blow out.

  | Mode | Level 0 (R,G,B,mono) | Level 100 (R,G,B,mono) |
  |---|---|---|
  | Golden hour | 1.00, 0.96, 0.88, 0.00 | 1.00, 0.62, 0.12, 0.00 |
  | Amber | 1.00, 0.93, 0.82, 0.00 | 1.00, 0.45, 0.02, 0.85 |
  | Astronomic | 1.00, 0.92, 0.90, 0.15 | 1.00, 0.25, 0.05, 1.00 |

  The Astronomic level-100 matrix is the one the owner tested before this project started; don't "improve"
  these numbers without asking — they were tuned on his screen.
- **Level is 0–100 everywhere.** **Level 0 is not "no tint"** — it's the weakest look. Anything that fades
  must blend the whole matrix toward identity (`TintMatrix.Blend`), never ramp the level number.
- **Mixing:** `TintMatrix.Mix` blends several looks by weight and fills leftover weight with an untinted
  screen; weights over 1 are shared out. Used where one filter hands over to the next.
- **Fades:** one constant, `Fade.DefaultSeconds` — **currently 0 (no fade), the owner's choice for now.**
  It covers on/off in both modes, filter hand-overs, and a schedule transition of 0 minutes. The schedule's
  own transition setting still fades over the minutes it names.
- **Live preview:** while dragging a level slider or a curve point, the screen shows that tint immediately,
  whatever the on/off state. Releasing snaps straight back to the real state (`TintPlan.SnapTo`).
- Applying is skipped when the matrix barely changed, so a slow fade doesn't hammer the display pipeline.
- The timer checks twice a second while fading, every 5 s otherwise, and not at all when there's nothing
  scheduled. Idle CPU measured at 0 ms over 20 s.

## Behaviour rules

- **Two app modes: simple and advanced.** Each keeps its own settings. The header button switches between
  them and shows the *other* mode's name.
- **Turning one mode on turns the other off.** Only one runs at a time.
- **OFF turns the mode off completely** — a schedule does not resume it. **ON re-arms the schedule:** the
  tint then appears only when the schedule says so. There is deliberately no "tint right now" override;
  users switch the schedule off for that.
- The tray's "Turn on/off" acts on whichever mode is current.
- **Simple mode:** one mode at a time (Golden hour / Amber / Astronomic), one level, and an optional
  schedule of one range. The schedule and level belong to the screen, not to the mode: switching mode
  keeps them. The range snaps to 15 minutes, may run past midnight, is never shorter than 15 minutes, and
  start == end means the whole 24 h. An edge never jumps over the other one — you fill the day by dragging
  the long way round. **Transition** is a dropdown in minutes: max 30, and never more than half the range.
- **Advanced mode:** several filters, each a time range with a mode and either a flat level or a curve.
  - Adding puts a 1 h filter in the left-most free slot, shorter if that's all that fits, minimum 15 min.
    When no free stretch of 15 min is left, the add menu greys out.
  - Filters never overlap: dragging an edge stops at the neighbour (2 px visual gap only, no time gap).
  - **Curve and flat level are two separate settings.** Expanding the curve applies it; collapsing goes
    back to the flat level and keeps the curve for next time.
  - Curve: points snap to 15 min and snap level with a neighbour within 3; the first and last points are
    locked to the ends and only move up and down; left click adds, right click removes.
  - Hand-over: a gap between filters fades out and back in; two filters that touch cross-fade, so the
    screen never flashes back to blue. (Both are instant while the fade constant is 0.)

## Settings file

`%AppData%\Amber\settings.json`, written ~500 ms after the last change. Missing or corrupt → defaults.

```json
{
  "IsAdvanced": false, "AdvancedIsOn": false,
  "Filters": [ { "Mode": "GoldenHour", "Start": "08:00:00", "End": "16:00:00",
                 "Level": 50, "CurveEnabled": true,
                 "Curve": [ { "Minutes": 0, "Level": 50 }, { "Minutes": 480, "Level": 75 } ] } ],
  "IsOn": false, "Mode": "Amber", "Level": 50,
  "ScheduleEnabled": false, "ScheduleStart": "20:00:00", "ScheduleEnd": "08:00:00",
  "TransitionMinutes": 0
}
```

`Mode` is one of `GoldenHour`, `Amber`, `Astronomic`. Curve points are minutes from the filter's start.
`Filters` is a read-only collection, so it needs `[JsonObjectCreationHandling(Populate)]` — without it,
filters silently fail to load.

## Code map

| File | What it does |
|---|---|
| `Tint/ScreenTint.cs` | Magnification API calls; apply and reset |
| `Tint/TintMatrix.cs` | Builds, blends and mixes the colour matrices |
| `App.xaml.cs` | Decides what the screen should show, the timer, tray wiring, single instance, preview |
| `TintPlan.cs`, `Fade.cs` | Eases the tint in/out; snaps back after a preview |
| `Schedule.cs` | Simple mode: strength at a moment, with the transition fades |
| `AdvancedSchedule.cs` | Advanced mode: which filters show at a moment and how strongly |
| `ScheduleRange.cs` | Range maths on the 24 h clock (length, snapping, moving an edge) |
| `FilterLayout.cs` | Where a new filter fits; edges stopping at neighbours |
| `CurveMath.cs` | Add / remove / move curve points, snapping, refitting after a range change |
| `Filter.cs`, `Settings.cs` | The data, change notification, JSON load/save |
| `TrayIcon.cs` | `Shell_NotifyIcon` interop, native right-click menu (no WinForms) |
| `StartupRegistration.cs` | "Start with Windows" via the HKCU Run key, launching with `--tray` |
| `Controls/*` | Drawn controls: level slider, simple schedule bar, advanced timeline, curve editor |
| `Views/*`, `MainWindow`, `Theme.xaml` | The screens and all styles/icons |
| `tools/make-icons.ps1` | Regenerates the tray/app icons from the logo shape |

**Everything with real logic is covered by tests** (`tests/Amber.Tests`, 60 of them): matrices, schedule,
ranges, layout, curve maths, fades, settings round-trip. Keep it that way — the owner can't review code,
so tests are the safety net. UI drawing code isn't tested; it's checked by screenshot.

## UI

Light theme only, 690 px wide, not resizable, max 800 px tall; the header (and the advanced timeline)
stay put while the rest scrolls. Settings and About cover the whole window and close with a ✕.
Hovers and state changes animate ~0.3 s; the curve graph fades in when a card expands.
Font: **Plus Jakarta Sans** (SIL OFL, bundled, licence in `Licenses.txt`). The Figma file uses Satoshi,
whose licence doesn't allow shipping the font — don't switch back.

> **The design is being redone.** The current code follows "Design 2.0" in Figma
> (file `yq4CMAFmPnS3ZclM1kvkAy`), but the owner is changing it. **Before any UI work, re-read the current
> Figma page** — don't trust pixel values in the code or in this file. Behaviour rules above still hold.

Copy rules: improve the English of any UI text, **except** the mode names (Golden hour, Amber, Astronomic)
and the About credit ("Credit: Dmitriy Ratushniak"), which stay exactly as they are.

## Working here

```
dotnet run --project src\Amber                 # run it (add -- --tray to start hidden)
dotnet test --artifacts-path <temp folder>     # build + tests, without touching bin\
```

- **The owner often has Amber running**, which locks `bin\`. Always build and test with
  `--artifacts-path` pointing at a temp folder.
- **Never launch Amber with his real settings** — it can tint his screen. Check no `Amber` process is
  running first (a second copy just exits), back up `%AppData%\Amber\settings.json`, run with a copy that
  has `"IsOn": false`, and restore afterwards.
- Screenshots of the running app: `PrintWindow` with `PW_RENDERFULLCONTENT`, driven from PowerShell, and
  UI Automation to click around. **Give the window focus first** or clicks go nowhere and you'll chase
  ghosts.
- CI: GitHub Actions builds and runs the tests on every push (`.github/workflows/ci.yml`).
- One plan step per branch, merged into `main` after the owner approves it.

## Where things stand

Done: tint engine, tray, single instance, start with Windows, settings, simple mode with its schedule,
the schedule engine, advanced mode (timeline, filter cards, curve editor), Settings and About.

Next:
1. **UI polish** against the redesigned Figma, plus whatever the owner lists as broken.
2. **Installer** and auto-update, with the "Check for updates" button in Settings wired up
   (it does nothing today), and `Licenses.txt` extended with the app's own licence.
3. **Beta, then release** on GitHub.

Open question for the owner: the repo is private; it becomes public and gets a licence at release.
