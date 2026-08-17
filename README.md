# fDimmer

**English** · [Русский](README.ru.md)

Screen dimming for Windows 10/11 that covers **everything** — the Alt+Tab switcher, the Start
menu, the taskbar, toast notifications, other applications' windows (elevated ones included),
and every monitor at once.

## How it differs from DimScreen and friends

Ordinary dimmers draw a translucent black window on top of the screen. Such a window can never
cover system surfaces: Alt+Tab, Start, the taskbar and notifications live in higher **window
bands** (`ZBID_IMMERSIVE_*`, `ZBID_SYSTEM_TOOLS`) that a plain `HWND_TOPMOST` window cannot
reach. The only overlay-based way around that is the undocumented `SetWindowBand()`, which
requires UIAccess — a code-signed binary installed under `%ProgramFiles%`.

fDimmer takes a different route: the **Magnification API**, specifically
`MagSetFullscreenColorEffect`. It sets a 5×5 color matrix that DWM applies to the final desktop
composition. What gets dimmed is the output itself, not a window on top of it — so everything
the compositor draws is covered. Windows' own "Color filters" feature works the same way.
No administrator rights and no UIAccess required.

## Two engines

| Engine | What it dims | When to use |
|---|---|---|
| **Global** (default) | everything: windows, Alt+Tab, Start, tray, notifications, all monitors | the main scenario |
| **Overlay** | selected monitors only; system UI stays bright | dimming one monitor out of several |

Switchable from the tray menu and the settings window. If the global engine is unavailable
(screen magnifier disabled by policy), the app falls back to the overlay and says so.

## Controls

- **Mouse wheel over the tray icon** — changes brightness by a configurable step.
- **Click the icon** — menu: on/off, presets 100/75/50/35/20 %, engine and monitor selection,
  settings, language, autostart, exit.
- **Settings** — brightness slider, lower limit, wheel step, transition time, on-screen
  indicator, interface language, start with Windows.

Interface language follows Windows by default and can be forced to English or Russian.
Settings live in `%AppData%\fDimmer\settings.json`.

## Schedule

Brightness can follow the clock. A schedule is a list of points — each one stays in effect
until the next, and the day wraps around midnight:

| Time | Brightness |
|---|---|
| 21:00 | 60 % |
| 00:00 | 40 % |
| 08:00 | 100 % (no dimming) |

With this schedule the screen is at 60 % from 21:00 to midnight, at 40 % from midnight until
08:00, and undimmed for the rest of the day. Edit the points in **Schedule…** in the tray menu.

A level is applied only at the moment a point comes due, so a manual change — the wheel, a
preset, the slider — holds until the next point rather than being overwritten a moment later.

## Guard against a screen you can no longer see

Brightness never drops below a configurable limit (15 % by default, hard minimum 5 %).
The screen returns to normal when the app exits, when the session ends, on an unhandled error,
and when the process is force-killed — the color effect lives in the process context and dies
with it.

The level is re-applied after the session is unlocked and after a display configuration change.

## Known limitations

- **The UAC prompt and Ctrl+Alt+Del** (secure desktop) are not dimmed — that is a separate
  desktop, out of reach for any user-mode application.
- **Exclusive-fullscreen games** bypass DWM and are not dimmed.
- **Conflicts with Windows "Color filters" and Night Light** — they use the same color-effect
  slot; the last one enabled wins.
- **The hardware mouse cursor** is not dimmed.
- Wheel handling requires the tray icon to be **pinned** in the notification area rather than
  hidden in the overflow: the shell does not report coordinates for a hidden icon.

## Build

```bash
dotnet build fDimmer.sln -c Release
```

Single file:

```bash
dotnet publish src/fDimmer/fDimmer.csproj -c Release -o publish
```

Requires .NET 9 (framework-dependent). Target platform is x64.

## Code layout

```
src/fDimmer/
  Program.cs                    single instance, emergency effect removal
  Interop/Magnification.cs      P/Invoke to magnification.dll, the color matrix
  Interop/NativeMethods.cs      windows, monitors, tray icon, low-level mouse hook
  Core/DimController.cs         state, smooth ramp, system events, engine fallback
  Core/MagnificationEngine.cs   global engine
  Core/OverlayEngine.cs         overlay engine
  Core/TrayWheelHook.cs         wheel over the tray icon
  Core/Strings.cs               English / Russian interface strings
  Core/Settings.cs, AutoStart.cs
  UI/TrayApplicationContext.cs  menu and wiring
  UI/TrayIcon.cs                icon via Shell_NotifyIcon (needs its own hWnd and uID)
  UI/DimOverlayForm.cs          click-through window per monitor
  UI/OsdForm.cs                 level indicator
  UI/SettingsForm.cs
```

## License

[MIT](LICENSE)
