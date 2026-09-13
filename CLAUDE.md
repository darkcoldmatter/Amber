# Amber — project notes

Windows blue-light filter. C# / WPF on .NET 10, targets Windows 10 and 11 (x64).

## Decisions
- **Tint engine:** Magnification API `MagSetFullscreenColorEffect` (5×5 color matrix, no admin, no overlay). Always reset to identity on every exit path. Tested: Windows also clears the tint if the process is killed; tint survives lock/sleep; cursor is not tinted.
- **UI:** fully custom design from Figma (file `yq4CMAFmPnS3ZclM1kvkAy`, page "Design 1.0"). Light theme only.
- **Priorities:** lightweight, minimal CPU/memory, simple code. App lives in the tray; window is optional.
- **Schedule:** fixed start/end time for the selected mode, fades in/out. Manual "off" lasts until the next start time.
- **Time picker (1.0):** plain/standard controls are fine; a scroll picker can come later.

## Copy rules
- Improve the English of all UI text, **except**: mode names (Golden hour, Amber, Astronomic) and the About credit ("Dmitriy Ratushniak") — keep exactly.

## Working agreement
- The owner is not a developer: explain steps plainly, keep changes small, one plan step per branch.
- Don't start the next plan step until the owner has checked the current one.
