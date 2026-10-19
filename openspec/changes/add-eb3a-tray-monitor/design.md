# Design

## Context

Greenfield repo. A throwaway spike in `local/spike` (gitignored) proved the transport on the user's EB3A:

- Advertises as `EB3A<serial>`; GATT connects unpaired, with no PIN or encryption.
- Service `0000ff00`: write `0000ff02`, notify `0000ff01`. Requests are Modbus RTU frames (addr 1, func 3, CRC16 low byte first); a response can arrive split across several notifications and is complete once `3 + byteCount + 2` bytes are buffered.
- Reading registers 10-49 and 70-90 about every 2 s was stable for several minutes. Initial connects sometimes fail and succeed on retry.
- Registers used (cross-checked with the community `bluetti_mqtt` EB3A map and a live dump): 10-15 device type (ASCII), 17-20 serial, 23-24 ARM firmware, 25-26 DSP firmware, 36 DC in W, 37 AC in W, 38 AC out W, 39 DC out W, 43 battery %, 48/49 AC/DC output on, 77 AC in voltage (x0.1 V), 86 internal DC in voltage (x0.01 V). There is no time-remaining or charging register.
- Settings registers that are readable (not written by this app): 3034 LED mode (1 low, 2 high, 3 SOS, 4 off), 3063 ECO on, 3064 ECO shutdown hours (1-4), 3065 charging mode (0 Standard, 1 Silent, 2 Turbo), 3066 power lifting. Confirmed on the user's firmware (ARM 2063.02, DSP 2052.07): a dump returned 3034=4, 3063=1, 3064=1, 3065=0, 3066=0.
- Identity decoding confirmed: registers 10-15 are ASCII (`EB3A`), 17-20 are the serial as little-endian words (`2240000000001`, matching the advertised name), 23-24 and 25-26 are firmware versions as low word + high word x 65536, divided by 100.
- Observed data: unplugged, AC in = 0 W and AC voltage reads ~2.6 V of noise; plugged in, AC voltage ~228 V and AC in = AC out + ~264 W of charge (UPS bypass). The unit is the 230 V variant with one AC socket.
- The manual confirms UPS bypass (mains passes straight to the outputs and charges the unit) and ECO (outputs off after a set time at 10 W or less).

See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Small code, native to Windows, using only what ships with .NET (WinRT, WinForms, WPF); no third-party BLE or UI framework.
- Correct, glanceable battery, runtime and charging state; survive Bluetooth drops, PC sleep and Bluetooth radio toggles unattended.
- A flyout that visibly shows the recognised model with a small live glyph of the station, not just a name.

**Non-Goals:**
- Writing registers, EcoFlow or other Bluetti models, MQTT/Home Assistant, time-to-full estimates, an installer, automatic PC shutdown/hibernate on low battery (later feature).

## Decisions

1. **.NET 10 LTS, WinRT Bluetooth directly** (`net10.0-windows10.0.19041.0`). .NET 9 (used by the spike) leaves support in November 2026, so the app targets the current LTS (supported to November 2028). `Windows.Devices.Bluetooth` is called without a wrapper library, as the spike already does. Alternatives: Rust with windows-rs (more boilerplate for tray, toasts and vector UI), Go/TypeScript (third-party BLE bindings, less native), Electron/Tauri (heavy for a tray app). .NET is the only option where BLE, tray, toasts and a vector-capable UI are all in-box.

2. **WinForms `NotifyIcon` for the tray, WPF for the flyout and the context menu** (`UseWindowsForms` + `UseWPF`, both part of the Windows Desktop runtime). WPF is chosen over a WinForms flyout because it renders vector geometry natively and DPI-perfectly, supports data binding of shape fills to state, and gives smooth rounded/acrylic-like styling; WinForms would need a third-party SVG library or hand-written GDI+ paths. The context menu is a styled WPF `ContextMenu` too: the WinForms `ContextMenuStrip` renders classic Win32 chrome, light only, which broke the Windows 11 look. WinUI 3 is rejected: it needs the Windows App SDK runtime and is awkward without packaging.

3. **Tray icon drawn at runtime.** The percentage is rendered with GDI+ into an icon sized from the system small-icon metric (DPI aware), in a colour chosen from `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme` (re-read on `UserPreferenceChanged`). Normal: monochrome number. Charging: number plus a small bolt or green accent. Low: amber/red. Disconnected: dimmed battery outline with no number. 100% is shown as a full-battery glyph, not text, because three digits do not fit at 16 px. Old `HICON`s are destroyed after replacement to avoid a GDI handle leak.

4. **Tooltip is text only**, built as `<model> - <pct>% - <W> W - ~<runtime>` (or `charging +<W> W` / `on mains` / the disconnected reason) and truncated to 127 characters (the NotifyIcon limit).

5. **Flyout = borderless, topmost WPF window** anchored above the tray icon (using the taskbar edge from `SHAppBarMessage`), with Windows 11 rounded corners via `DWMWA_WINDOW_CORNER_PREFERENCE`, following the light/dark theme, closing on deactivation or Escape. Layout (see `design/upsy.design.html`): 104x96 station glyph on the left; percentage, runtime or charging state, and power on the right; a collapsible "Details" section with every decoded value and identity data.

6. **Station glyph is our own small vector drawing (104x96), keyed by model.** It is a logo-sized pictogram, not a large illustration.
   - Why not a photo: product photos are Bluetti's copyrighted marketing assets (not redistributable in a public repo), raster images blur at 150-200% DPI, do not adapt to dark theme, and cannot show live state.
   - The source drawing is the owner's own outline `assets/station/eb3a-outline.svg`; `tools/gen_station_glyph.py` derives `assets/station/eb3a.svg` and the WPF shapes from it, with a live half-ring battery gauge on the station screen and named parts: `battery-fill`, `ac-out` (one socket), `dc-out` (USB-A, USB-C, car socket and wireless pad share the one DC switch), `ac-in`, `dc-in`, `display`.
   - It is rebuilt as WPF XAML with the same geometry (SVG path data is compatible with WPF path markup), so no SVG library is needed at runtime.
   - Parts are bound to telemetry: battery fill height = battery %, coloured by state; AC/DC output groups highlighted when their output-on flag is set and labelled with their watts; the active input highlighted with its watts while power flows in.
   - The model string read from registers 10-15 selects the drawing; an unknown model falls back to a generic battery drawing plus the raw model string. This is the visible proof that the app recognised the station.

7. **Notifications via `NotifyIcon.ShowBalloonTip`**, which Windows 10/11 renders as toasts for unpackaged apps. This avoids packaging, AUMID shortcuts and extra NuGet packages. Limitation accepted for phase 1: no action buttons, and Do Not Disturb / Focus suppresses them (see Risks).

8. **Layering for testability**: (a) transport (scan, connect, send/receive frames, reassembly), (b) protocol (CRC, frame building, validation) and register decoder to a plain `Telemetry` record, (c) pure `PowerState` logic (classification, smoothing, runtime, alert edge detection) with an injected clock, (d) UI. (b) and (c) carry unit tests on plain values, so no Bluetooth hardware is needed.

9. **State rules** (thresholds are named constants):
   - **On mains** = AC input voltage > 100 V. Voltage alone is used because when the battery is full and there is no load, AC input power can legitimately be 0 W while still plugged in; using power would raise a false power-loss alert. Unplugged reads ~2.6 V.
   - **Net battery power** = (AC in + DC in) - (AC out + DC out). **Charging** = net > 10 W and battery < 100%. This also covers solar/car input on DC.
   - **Load present** = AC out + DC out > 20 W.

10. **Runtime formula**: `hours = 268 Wh x usable x battery% / smoothed discharge W`, where discharge = (AC out + DC out) - (DC in), smoothed by an exponential moving average with a ~60 s time constant. `usable` starts at 0.85 (inverter losses) and is a constant to calibrate against an observed discharge. Shown with "~", hidden until about 10 s of samples exist, and shown as "> 24 h" above that. On mains, while charging, or without a load, the state is shown instead. The EMA is reset on reconnect.

11. **Alerts**: low-battery fires on the downward crossing of the threshold (default 20%) while on battery and re-arms when the level rises above the threshold or mains returns; if the first snapshot after the app starts is already at or below the threshold on battery, it fires once. Re-arm state is kept across reconnects so a flapping link does not repeat the alert. Power lost / restored fire when the on-mains state has held for 5 s. Edge detection starts only after the first snapshot of a connection (no alert for the state found at connect) and nothing fires while disconnected.

12. **Connection lifecycle**: one long-running loop with states `RadioOff`, `Scanning`, `Connecting`, `Polling`, `Paused`. Requests are strictly sequential with a 4 s timeout; 3 consecutive failures or `ConnectionStatusChanged` to disconnected return to `Connecting` with backoff 2 s doubling to 30 s. The station address is cached in settings, and scanning by name prefix is the fallback when the cached address fails. The loop watches the Bluetooth radio state and `SystemEvents.PowerModeChanged`; on resume from sleep it drops the old GATT session and reconnects. Each cycle reads 10-49 and 70-90; identity is decoded from the first cycle and the settings registers (3034, 3063-3066) are read once per connection and then once a minute, skipped silently if the station does not answer them.

13. **Pause monitoring**: "Pause for 30 minutes" (context menu and flyout footer) disposes the GATT session so the Bluetti phone app can connect (the station accepts only a limited number of BLE clients). It resumes by itself after 30 minutes, or earlier with Resume, because a forgotten pause would silence a real outage (UX review finding). Paused has its own tray icon (pause bars) and says when it resumes.

14. **Settings and lifecycle**: JSON in `%APPDATA%\upsy\settings.json` (station address, low threshold). Start with Windows is the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\upsy` value, read back as the source of truth rather than stored in JSON. Single instance via a named mutex. Unhandled exceptions are logged to `%LOCALAPPDATA%\upsy\upsy.log` (small, rotated) so hours-long runs can be diagnosed.

15. **Design source of truth**: `design/upsy.design.html` (accepted), recorded in DESIGN.md. Reviewed with the impeccable detector (no findings) and `/ux-crux-review` (smart). Fixes taken from that review: paused tray icon and auto-resume, a critical toast at 10%, runtime rounding, keyboard activation of the icon, and a bold default "Open upsy" menu item.

16. **Alert levels**: the low alert fires at the user threshold (10-30% in 5% steps, default 20%); a second, fixed critical alert fires once at 10% with the same re-arm rules. The tray icon turns amber at the threshold and red at 10% or less.

17. **Runtime display**: rounded to 5 minutes below one hour and to 10 minutes above, always prefixed "about", so the estimate does not suggest precision it does not have.

18. **Keyboard and accessibility**: the tray icon opens the flyout on Enter/Space when focused with Win+B, the flyout's buttons have automation names, Tab order follows the visual order, Escape closes it.

19. **Tooling**: `Directory.Build.props` (nullable, warnings as errors, latest-recommended analyzers, code style enforced in build), `Directory.Packages.props` (central versions), `.editorconfig`, `global.json` (SDK 10.0.4xx), xUnit v3 tests, and a GitHub Actions workflow on `windows-latest` running build, test and `dotnet format --verify-no-changes`.

## Risks / Trade-offs

- [EB3A may switch BLE off after long inactivity] -> Treat as disconnected with backoff scanning; verify over hours before release.
- [Phone app or another BLE client holding the connection] -> Show disconnected and keep retrying; offer Pause for 30 minutes so the user can hand the link over.
- [Runtime estimate error: load varies, inverter efficiency and battery curve are approximate, battery % has 1% steps] -> Smooth, label it approximate, and calibrate `usable` from a logged discharge.
- [Notifications are suppressed under Do Not Disturb / Focus] -> Accept for phase 1 and document it; the icon still turns amber/red. Moving to Windows App SDK toasts with urgent priority is the upgrade path.
- [Firmware changes could add encryption, as reported for newer Bluetti firmware] -> Spike passed on the user's current firmware; failure surfaces as the disconnected state with a log entry.
- [WPF adds memory (~40-60 MB working set vs ~20-30 MB WinForms only)] -> Acceptable for a desktop tray app; the flyout window is created on first open and reused.
- [EB3A port layout differs by region (one or two AC sockets)] -> The drawing is matched to the user's 230 V unit; other variants still work, only the drawing may differ.
- [Tray icon text is tiny at 16 px] -> Two-digit numbers plus a full-battery glyph; check at 100-200% DPI.

## Resolved Questions

- Settings registers 3034 and 3060-3066 answer reads on the user's firmware (see Context).
- The user's EB3A has one AC socket; the glyph draws one.
- Automatic hibernate/shutdown is a later feature, not phase 1.
