# Design

Recorded from the built mock-up `design/upsy.design.html` (accepted 2026-10-12). Product truth is in PRODUCT.md.

## World

Native Windows 11 (Fluent). upsy should feel like a part of the taskbar, not an app with its own chrome. Brand lives only in the mascot, the app icon and the wordmark.

## Surfaces

| Surface | Spec | Notes |
|---|---|---|
| Tray icon | 16 px at 100% (small-icon metric), redrawn per DPI | Percentage digits, bold, taskbar-theme ink. 2 px bar under the digits: green = charging, amber = at/below threshold, red = ≤10%. Digits take the amber/red too. 100% = Segoe Fluent `EBAA` (full battery). No data = `EC02` (battery with a cross). Paused = `EBA0` (empty battery) with pause bars. Glyphs are rendered through WPF/DirectWrite because GDI+ drops this font at 20 and 24 px. Battery level only (EcoFlow advertisement, no session): the number over a dotted bar. With several stations the icon shows the tray station chosen by the user. |
| Tooltip | Native NotifyIcon text, ≤127 chars | `EB3A · 58% · 128 W out · about 1 h left`, `EB3A · 54% · charging +264 W`, `EB3A · 100% · on mains`, `upsy · Bluetooth is off`, `DELTA 2 · 84% · battery level only`. Several stations: the tray station's line, then one `<name> · <pct>%…` line per other station, dropped first at the limit. |
| Flyout | 360 px wide, 8 px radius, 1 px stroke, shadow 0 8 16 | Header grid: 104×96 station glyph, then model · source (12 px secondary), percentage (40 px semibold, "%" 20 px), one status line (14 px semibold with Fluent icon, semantic colour), one sub line (12 px secondary). Then In/Out strip (card), Details expander (grouped Power / Station / Device), footer 48 px (16 px app icon + "upsy", Pause/Resume and More icon buttons). |
| Several stations | Same frame, one section per station | Tray station first, marked with a pin (`E718`). Each section is the single-station layout plus a station More button (top right: Pause/Resume, Show in tray, Forget this station…). Footer: brand, Add station (`E710`), More. Forget asks once in a small window that names the station and says it can be added again. |
| Battery level only | Station section | Advertised % with a muted gauge, model line `DELTA 2 · battery level only`, no In/Out strip, no runtime, no alerts. Status + notice name the reason and one action: Sign in, Try again (key rejected), Open setup guide (key table missing), none (another app holds the link). |
| No-data flyout | Same frame | No percentage; status line 20 px; a notice card with the reason, the recovery and at most one button (Bluetooth settings / Resume). |
| Context menu | WPF menu styled with the flyout tokens (`Tray/TrayMenu.xaml`); 8 px radius, 4 px padding, 32 px rows | Opens on right-click at the pointer and from the flyout's More button above it. The stock WinForms menu was rejected: it ignores dark mode and Windows 11 metrics. | Title row (state), bold default "Open upsy", Pause for 30 minutes, Start with Windows (check), Low battery alert ▸ 10–30% in 5% steps, Exit. |
| Context menu, several stations | Same menu | Adds Pause `<tray station>` for 30 minutes, Show in tray ▸ (stations, current checked), Add station…, and Sign out of EcoFlow when signed in. |
| Sign-in window | 400 px, Windows 11 dialog, flyout tokens | Title, what is kept (only the user ID, encrypted for the Windows account) and what is not (password), Email, Password, error InfoBar with EcoFlow's message, Cancel / Sign in (accent, shows progress). Enter submits, Escape cancels. |
| Add station window | 400 px dialog | Supported stations advertising nearby: model, name or serial, signal, Add (accent) or Added; searching/empty state explains what is supported. |
| Toasts | Windows notifications via NotifyIcon | Title = `<station>: <event>`, body = what it means now; each station alerts on its own. Mains lost, Battery low (threshold), Battery critical (10%), Mains back. |

## Tokens

| Token | Light | Dark |
|---|---|---|
| flyout | #F9F9F9 | #2C2C2C |
| card | #FFFFFF | rgba(255,255,255,.05) |
| ink / ink-2 | #1B1B1B / #5C5C5C | #FFFFFF / #C8C8C8 |
| divider | rgba(0,0,0,.08) | rgba(255,255,255,.08) |
| accent (focus, active port tint, primary button) | #005FB8 | #60CDFF |
| ok | #0F7B0F | #6CCB5F |
| caution | #9D5D00 | #FCE100 |
| critical | #C42B1C | #FF99A4 |
| glyph (idle parts) / glyph-on | #5C5C5C / #1B1B1B | #9D9D9D / #FFFFFF |

Theme follows `SystemUsesLightTheme` (taskbar) for the tray icon and `AppsUseLightTheme` for the flyout.

## Type

Segoe UI Variable (Text for body, Display for the percentage). Icons: Segoe Fluent Icons, falling back to Segoe MDL2 Assets. Numbers use tabular figures.

## Station glyph

Source: `assets/station/eb3a-outline.svg`, the owner's outline of their EB3A (800×740). `tools/gen_station_glyph.py` derives the flyout glyph from it: labels and hairlines dropped, strokes thickened for 104×96, live parts tagged. It writes `assets/station/eb3a.svg` and the WPF shapes between the `station-glyph` markers in `FlyoutWindow.xaml`; edit the script, not the outputs.

DELTA 2: `assets/station/delta2-outline.svg` (owner's front view, 1086×1448) gives a 72×96 glyph centred in the same 104×96 box. Its live parts are the screen's half ring and the USB-A/USB-C panel (DC out, accent tint when on); AC outlets, 12 V and inputs are at the back, so their values live in the In/Out strip. A muted ring (ink-3) marks battery level only.

EB3A live parts: the half ring on the station screen fills to the battery level (tone colour; dashed track without data), the AC output and DC output (car socket + USB) blocks are drawn in glyph-on with an accent tint pad when switched on, the AC and DC inputs turn ok-green while feeding power, and the light turns glyph-on when its LED mode is not off.

## Brand

| | |
|---|---|
| Mascot | `assets/brand/upsy-mascot.svg`: a mustard battery with a teal "charge" belly and bolt, teal terminal cap with a daisy ("upsy-daisy"), thick ink outline, flat. Never inside status UI. |
| App icon | `assets/brand/upsy-icon.svg`: mascot bust on a cream tile with a rust sun. Used for the .exe, toasts and the flyout footer. |
| Wordmark | Fredoka 600, lower-case `upsy`. |
| Palette | Mustard #E3A72F, Teal #2F7F7A, Rust #D2602F, Olive #7A8F35, Cream #F6ECD6, Ink #3A2A22. |

## Copy rules

Plain, calm, specific. Runtime always "about", rounded to 5 min under an hour and 10 min above. Never show stale numbers; say why there is no data and what to do.
