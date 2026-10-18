# Amber — project notes

Amber is a lightweight Windows 10/11 app that reduces blue light by tinting the whole screen.
C# / WPF on .NET 10, x64. It lives in the tray; the window is optional.

**Owner:** Dmitriy (designer, not a developer). Explain things plainly, keep changes small, and don't
start the next step until he has checked the current one. He tests every step by hand on his own PC.

**Status:** the engine and every feature work end to end, and the UI is rebuilt on Figma "Design 3.0"
(steps 5a and 5b), with 10-second fades and cross-fades between advanced filters (step 5c) — all merged.
Step 6a (installer, auto-update, versioning, release pipeline) is built; the update notice (6b), going
public and the first release come next.

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
  | Deep night | 1.00, 0.92, 0.90, 0.15 | 1.00, 0.25, 0.05, 1.00 |

  The Deep night level-100 matrix (the mode was called "Astronomic" before Design 3.0) is the one the owner
  tested before this project started; don't "improve" these numbers without asking — they were tuned on his screen.
- **Level is 0–100 everywhere.** **Level 0 is not "no tint"** — it's the weakest look. Anything that fades
  must blend the whole matrix toward identity (`TintMatrix.Blend`), never ramp the level number.
- **Mixing:** `TintMatrix.Mix` blends several looks by weight and fills leftover weight with an untinted
  screen; weights over 1 are shared out. Used where one filter hands over to the next.
- **Fades:** `Fade.DefaultSeconds` — **0 (no fade), the owner's choice** — covers on/off in both modes and a
  simple-schedule transition set to "Off"; any other transition setting fades over the time it names.
  **Advanced filter edges always fade over 10 s** (`AdvancedSchedule`, no setting): each edge is a 10 s ramp
  centred on it (starts 5 s before, done 5 s after). Two filters that touch run their ramps over the same
  10 s with weights adding up to 1 — a cross-fade, so the screen never goes untinted between them. Any real
  gap (15 min at least) fades out to clear and back in.
- **Live preview:** while dragging a level slider or a curve point, the screen shows that tint immediately,
  whatever the on/off state. Releasing snaps straight back to the real state (`TintPlan.SnapTo`).
- Applying is skipped when the matrix barely changed (`TintMatrix.Differs`, 0.001 — a quarter of one 8-bit
  colour step), so a slow fade doesn't hammer the display pipeline but no 1 % step is ever swallowed.
- **Every visible change moves in steps of at most ~1 % (owner's rule).** The timer checks every 5 s, and not
  at all when there's nothing scheduled (idle CPU measured at 0 ms over 20 s). From one check before a fade
  until it ends it steps every fade length / 100 (100–500 ms; 100 ms for advanced 10 s fades), so fades
  glide and never start late. On a sloped curve it checks often enough to move at most 1 level at a time
  (`AdvancedSchedule.LevelTick`, 100 ms–5 s), and curves are read at fractional minutes. A whole-day
  schedule or filter has no edges and never fades. Switching ON (or editing) in the middle of a fade jumps
  straight to that moment's strength, by design.

## Behaviour rules

- **Two app modes: simple and advanced.** Each keeps its own settings. The "Advanced" switch in the header
  flips between them.
- **Turning one mode on turns the other off.** Only one runs at a time.
- **OFF turns the mode off completely** — a schedule does not resume it. **ON re-arms the schedule:** the
  tint then appears only when the schedule says so. There is deliberately no "tint right now" override;
  users switch the schedule off for that.
- The tray's "Turn on/off" acts on whichever mode is current.
- **Simple mode:** one mode at a time (Golden hour / Amber / Deep night), one level, and an optional
  schedule of one range, switched on and off by the header's Schedule button (a first-time schedule is
  12:00–13:00). The schedule and level belong
  to the screen, not to the mode: switching mode keeps them. The range snaps to 15 minutes, may run past
  midnight, is never shorter than 15 minutes, and start == end means the whole 24 h. An edge never jumps
  over the other one — you fill the day by dragging the long way round. **Transition** is one dropdown that
  mixes units — Off, 10 s, 30 s, 1, 2, 5, 10, 15, 30 min — never more than half the range; "Off" uses the
  fade constant (instant today). Stored in seconds.
- **Advanced mode:** several filters, each a time range with a mode and either a flat level or a curve.
  - Adding puts a 1 h filter in the left-most free slot, shorter if that's all that fits, minimum 15 min.
    When no free stretch of 15 min is left, the add menu greys out.
  - Filters never overlap: dragging an edge stops at the neighbour. Touching bars share one border line.
  - Click a bar to select it; its mode, times, level and curve button show below the timeline. Click it
    again, click empty track or press Esc to deselect; its trash icon or Delete removes it. With nothing
    selected, "+ Add filter" opens the mode menu. A new filter isn't selected: a one-time hint bubble points
    at the first one. The "How it works" card shows while there are no filters, until it's closed.
  - **Curve and flat level are two separate settings.** Expanding the curve applies it; collapsing goes
    back to the flat level and keeps the curve for next time.
  - Curve: points snap to 5 min (range edges stay on 15) and line up with any other point whose level is
    within 3; the first and last points are locked to the ends and only move up and down; left click adds,
    right click removes. Moving a range keeps the curve's shape relative to the filter (owner likes this).
  - Hand-over: a gap between filters fades out and back in over 10 s each; two filters that touch
    cross-fade over the same 10 s, so the screen never flashes back to blue.

## Settings file

`%AppData%\Amber\settings.json`, written ~500 ms after the last change. Missing or corrupt → defaults.

```json
{
  "SettingsVersion": 1, "SkippedVersion": null,
  "IsAdvanced": false, "AdvancedIsOn": false, "AdvancedIntroClosed": false, "FilterHintDone": false,
  "Filters": [ { "Mode": "GoldenHour", "Start": "08:00:00", "End": "16:00:00",
                 "Level": 50, "CurveEnabled": true,
                 "Curve": [ { "Minutes": 0, "Level": 50 }, { "Minutes": 480, "Level": 75 } ] } ],
  "IsOn": false, "Mode": "Amber", "Level": 50,
  "ScheduleEnabled": false, "ScheduleStart": "12:00:00", "ScheduleEnd": "13:00:00",
  "TransitionSeconds": 0
}
```

`SettingsVersion` (always written first) is for future format changes: old files without it read 0 and
still load. `SkippedVersion` is the update the user dismissed with ✕ — not announced again.
`Mode` is one of `GoldenHour`, `Amber`, `DeepNight` (an old file that still says `Astronomic` fails to
parse and loads as defaults). Curve points are minutes from the filter's start.
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
| `TimeFormat.cs` | Every time shown in the UI, in Windows' 12- or 24-hour format |
| `TextLook.cs` | Makes grey, unsnapped text smoothing the default for every element, menus included |
| `Updates.cs` | Update checks and installs through Velopack + GitHub Releases (see "Releases" below) |
| `TrayIcon.cs` | `Shell_NotifyIcon` interop, native right-click menu (no WinForms) |
| `StartupRegistration.cs` | "Start with Windows" via the HKCU Run key, launching with `--tray` |
| `Controls/*` | Level slider; simple (`ScheduleBar`) and advanced (`AdvancedTimeline`) timelines on the shared drawing (`Timeline`); `CurveEditor`; `TimeField`, `DropdownList`, `TrackedText` (letter-spacing), `Glow`, `HintBubble` |
| `Views/*` | `SimpleView`, `AdvancedView` (intro card, timeline, add menu or `FilterPanel`), `SettingsView`, `AboutView` |
| `MainWindow`, `Theme.xaml` | The shell (header, width switch, overlays) and all styles/icons (icons are exact Figma geometry) |
| `tools/make-icons.ps1` | Regenerates the tray/app icons from the logo shape |
| `tools/make-cursor.ps1` | Regenerates `Assets/resize.cur`, the double-arrow cursor for timeline edges |
| `app.manifest` | Per-monitor DPI awareness, Windows 10/11, runs without admin |
| `dotnet-tools.json` | Pins the `vpk` packaging tool (Velopack) used locally and in CI |
| `.github/workflows/release.yml` | Builds, packs and uploads a draft GitHub release when a `v*.*.*` tag is pushed |

**Everything with real logic is covered by tests** (`tests/Amber.Tests`, 187 of them): matrices, schedule,
ranges, layout, curve maths, fades, settings round-trip. Keep it that way — the owner can't review code,
so tests are the safety net. UI drawing code isn't tested; it's checked by screenshot.

## UI

Follows **Design 3.0** in Figma (file `yq4CMAFmPnS3ZclM1kvkAy`, page `75:303`; components and icons on
page `5:2086`). Before any UI work, re-read the Figma frames — don't trust pixel values in this file.

- Light theme only, not resizable. **724 px wide in Simple, 848 px in Advanced**: the window sizes itself to
  its content (so the inside matches the Figma frame) and keeps its centre when the width changes. Max 800 px
  tall; the header stays put and the rest scrolls. Settings and About cover the whole window and close with ✕.
- Header: settings and about buttons, the "Advanced" switch, the Schedule button (Simple only), and ON/OFF
  for the mode you're in.
- Simple mode: big level % above the level slider, mode chips at the bottom; with the schedule on, the
  timeline, the from/to time fields and Transition below.
- Advanced mode: the timeline's bars show a dome in the bar colour (`#FFE063` / `#FFAE5D` / `#FF7C7C` —
  brighter than the menu's mode colours `#FEC960` / `#F99E43` / `#FD867F`) and the level, or a small curve
  icon when the curve is on; content hides when it doesn't fit. Selected bar white with a trash icon, the
  others at 70 %. The curve opens as a white graph card under the filter panel (the slider hides).
- Fonts: **Plus Jakarta Sans** for words, **Onest** for every number (both SIL OFL, bundled, licences in
  `Licenses.txt`). The Figma file uses Satoshi, whose licence doesn't allow shipping it — never use it.
  WPF has no letter-spacing, so tracked text goes through `TrackedText`.
- Background `#F3F3F3` with a soft yellow glow (`Glow`: a small precomputed bitmap, no BlurEffect) at 70 % in
  Simple/Settings/About and 30 % in Advanced. It never changes with the mode (owner's choice).
- Timeline ranges sit exactly on their times (a thin border separates them, no gap); hover draws a thin dark
  outline; the double-arrow cursor shows only on a range's left or right edge. The Simple range has two grip
  lines, at its real start and end (none at a midnight cut).
- Times follow Windows' 12- or 24-hour setting (`TimeFormat`, read live from Windows each time the window
  opens — WPF pins the culture at startup, so `CultureInfo` alone would go stale). AM/PM is always written
  "AM"/"PM" in English, like the rest of the UI (localised designators get cut off or overlap). 12-hour:
  timeline labels "12 AM · 2 · … · 12 PM · …", curve labels with AM/PM on the first, last and 12 o'clock
  labels, time fields "6 : 00 PM" with one 12 AM–11 PM hour list. 24-hour looks exactly like the Figma frames.
  The owner's own PC is 24-hour (`ru-UA`); PowerShell 5.1's `Get-Culture` misreports it as `en-US`.
- Time fields: click the hours or the minutes for a short list; the mouse wheel steps them.
- Hovers and state changes animate ~0.3 s, except the ON/OFF button, which changes instantly (owner's
  choice); nothing moves on the timeline or inside the graph.
- Text is grey-smoothed with `TextHintingMode="Animated"`, made the default for every element at startup
  (`TextLook`): plain grayscale snaps letters to pixels and looks jagged at 100 % scaling.

Copy rules: improve the English of any UI text, **except** the mode names (Golden hour, Amber, Deep night),
which stay exactly as they are. The About screen names no creator (owner's choice: GitHub tells that story).
**Amber is openly "vibe coded"** (owner's wish): wherever the creator is named — README, notices, installer
publisher — credit Dmitriy and Claude together. The legal copyright line and SignPath's team
roles stay in Dmitriy's name only.

## Working here

```
dotnet run --project src\Amber                 # run it (add -- --tray to start hidden)
dotnet test --artifacts-path <temp folder>     # build + tests, without touching bin\
```

- **The owner often has Amber running**, which locks `bin\`. Always build and test with
  `--artifacts-path` pointing at a temp folder.
- **Never launch Amber with his real settings** — it can tint his screen. Check no `Amber` process is
  running first (a second copy just exits), back up `%AppData%\Amber\settings.json`, run with a copy that
  has `"IsOn": false` and `"AdvancedIsOn": false`, stop the test copy, and only then restore the original.
  Never click ON or drag a level slider in automated tests — that tints his screen too.
- Screenshots of the running app: `PrintWindow` with `PW_RENDERFULLCONTENT`, driven from PowerShell, and
  UI Automation to click around. **Give the window focus first** or clicks go nowhere and you'll chase
  ghosts.
- CI: GitHub Actions builds and runs the tests on every push (`.github/workflows/ci.yml`).
- **The terminal build stays the owner's quick test copy.** A copy run from a build folder knows it isn't
  installed: Settings shows "Not installed" and it never checks or updates, and it doesn't touch the
  "Start with Windows" entry on its own. It shares the single-instance lock with an installed Amber, so quit
  the installed one (tray → Quit) before `dotnet run` on the same PC.
- One plan step per branch, merged into `main` after the owner approves it.

## Releases

- **Installer and updates: Velopack** (MIT) + GitHub Releases. Self-contained win-x64 (nothing else to install),
  per-user install to `%LocalAppData%\Amber` (no admin), Start-menu shortcut, uninstall from Windows Settings →
  Apps (it also removes the "Start with Windows" entry; settings stay). Setup ≈ 69 MB; updates download as
  small deltas. Publisher shown: "Dmitriy Ratushniak & Claude".
- An installed Amber checks GitHub a minute after it starts, then every 24 h, and when Settings → Check is
  clicked. Automatic failures are silent (retried after an hour). Installing resets the tint, quits, lets the
  updater swap the app and restarts it — into the tray if the window was closed.
- **Update notice** (Figma `125:292`): when a version is on offer, Simple shows "A new version is available"
  with Update and ✕ at the top right; Advanced shows it as one row under the header and moves the rest down.
  Update shows the download % and then restarts; ✕ skips that version (`SkippedVersion`); Settings still
  offers it. Preview it from the terminal build: `dotnet run --project src\Amber -- --pretend-update 1.0.1`
  (add `--pretend-fail` for the failed download) — Debug builds only, never in the installed app.
- **One version number:** `<Version>` in `src/Amber/Amber.csproj`; About shows it in full.
- **To release:** set `<Version>` (e.g. 1.0.1), commit, push; `git tag v1.0.1` and push the tag; GitHub Actions
  builds a **draft** release; write the notes on GitHub and press Publish. Publish or delete each draft before
  tagging the next version (the delta is built from the previous release).
- **Licence:** GPL-3.0-or-later (`LICENSE`, shipped as `LICENSE.txt`); the name "Amber" and the icon aren't
  covered (README). Third-party notices in `src/Amber/Licenses.txt`. Code signing is planned through the
  SignPath Foundation (OSI licence, public repo, a first release, CI builds, approve each release); the
  workflow has marked places for it.

## Where things stand

Done and merged: tint engine, tray, single instance, start with Windows, settings, simple mode and its
schedule, advanced mode with curves, Settings and About — all on Figma Design 3.0 (steps 5a–5b), with
10-second fades and cross-fades between advanced filters and the 1 % smoothness rule (5c).
Built on branch `step-6a-installer-updates`: installer and auto-update (Velopack), version 1.0.0, release
workflow, GPL-3.0 licence, public README, and the in-app update notice (owner's Figma frame `125:292`).

Next:
1. Owner checks step 6a + the update notice; merge.
2. Before going public: rewrite the history so every commit uses the owner's GitHub no-reply address
   (`328485409+darkcoldmatter@users.noreply.github.com`) and delete the old merged step branches; then the
   repo becomes public (owner confirms the switch).
3. First release v1.0.0 (unsigned), then the owner applies to SignPath Foundation; later releases signed.
4. Owner's designs still to come: new app and tray icons.

Decided: free and open source (GPL-3.0-or-later), public repo, SignPath Foundation signing, self-contained
installer, automatic update checks with an in-app notice.
