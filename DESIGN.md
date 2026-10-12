# Design

Recorded from the built mock-up `design/upsy.design.html` (accepted 2026-10-12). Product truth is in PRODUCT.md.

## World

Native Windows 11 (Fluent). upsy should feel like a part of the taskbar, not an app with its own chrome. Brand lives only in the mascot, the app icon and the wordmark.

## Surfaces

| Surface | Spec | Notes |
|---|---|---|
| Tray icon | 16 px at 100% (small-icon metric), redrawn per DPI | Percentage digits, bold, taskbar-theme ink. 2 px bar under the digits: green = charging, amber = at/below threshold, red = ≤10%. Digits take the amber/red too. 100% = full-battery glyph. No data = crossed-out battery outline, dimmed. Paused = battery outline with pause bars. |
| Tooltip | Native NotifyIcon text, ≤127 chars | `EB3A · 58% · 128 W out · about 1 h left`, `EB3A · 54% · charging +264 W`, `EB3A · 100% · on mains`, `upsy · Bluetooth is off`. |
| Flyout | 360 px wide, 8 px radius, 1 px stroke, shadow 0 8 16 | Header grid: 96×72 station glyph, then model · source (12 px secondary), percentage (40 px semibold, "%" 20 px), one status line (14 px semibold with Fluent icon, semantic colour), one sub line (12 px secondary). Then In/Out strip (card), Details expander (grouped Power / Station / Device), footer 48 px (16 px app icon + "upsy", Pause/Resume and More icon buttons). |
| No-data flyout | Same frame | No percentage; status line 20 px; a notice card with the reason, the recovery and at most one button (Bluetooth settings / Resume). |
| Context menu | Native Win11 menu metrics | Title row (state), bold default "Open upsy", Pause for 30 minutes, Start with Windows (check), Low battery alert ▸ 10–30% in 5% steps, Exit. |
| Toasts | Windows notifications via NotifyIcon | Title = event, body = what it means now. Mains lost, Battery low (threshold), Battery critical (10%), Mains back. |

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

`assets/station/eb3a.svg`, 96×72, 2 px round strokes in `currentColor`. Parts: handle, body, display with `battery-fill` (width 22 × %), `ac-out` (one socket, the 230 V unit), `dc-out` (USB-A ×2, USB-C, car socket as one group), `ac-in` (left), `dc-in` (right). Active output = glyph-on stroke + accent tint pad; feeding input = ok colour; battery-fill = ink, or the state colour.

## Brand

| | |
|---|---|
| Mascot | `assets/brand/upsy-mascot.svg`: a mustard battery with a teal "charge" belly and bolt, teal terminal cap with a daisy ("upsy-daisy"), thick ink outline, flat. Never inside status UI. |
| App icon | `assets/brand/upsy-icon.svg`: mascot bust on a cream tile with a rust sun. Used for the .exe, toasts and the flyout footer. |
| Wordmark | Fredoka 600, lower-case `upsy`. |
| Palette | Mustard #E3A72F, Teal #2F7F7A, Rust #D2602F, Olive #7A8F35, Cream #F6ECD6, Ink #3A2A22. |

## Copy rules

Plain, calm, specific. Runtime always "about", rounded to 5 min under an hour and 10 min above. Never show stale numbers; say why there is no data and what to do.
