# Product

<!-- impeccable:product-schema 1 -->

> Written during an unattended session from the owner's brief and the OpenSpec change `add-eb3a-tray-monitor`. Facts marked *(inferred)* were not confirmed by the owner yet.

## Platform

Windows desktop (native .NET WPF + WinForms tray app; Windows 10 19041+ and Windows 11). Not a web product: the HTML design canvas in `design/` is a mock-up of native UI.

## Stack

.NET 10 LTS, C#, WinRT Bluetooth LE, WinForms `NotifyIcon`, WPF flyout. Chosen by the owner (.NET) and refined in the change design (WPF for vector rendering).

## Users

One person (the owner) running a Windows PC on a Bluetti EB3A that acts as a UPS. They are at the desk, working or gaming; the station is out of sight under or beside the desk. The job: know at a glance how much charge and runtime are left, whether mains is present, and be warned before the PC dies. *(inferred: no other audience yet; the repo may be shared publicly as an open-source tool)*

## Product Purpose

`upsy` reads the EB3A over Bluetooth LE and turns it into a proper UPS indicator in the Windows notification area: percentage, remaining runtime, charging state, and alerts on power loss and low battery. Success: the owner never has to look at the station's own screen, and never loses work to a silent drain.

## Positioning

There is no official Bluetti Windows app and no tray tool. `upsy` works unpaired, read-only, with nothing installed but itself, and derives what the station does not report (runtime, charging, mains presence).

## Operating Context

- Glanced at in the taskbar many times a day; opened (flyout) a few times a day; alerts arrive rarely and matter a lot.
- Runs for days; the PC sleeps and wakes; Bluetooth can drop; the phone app sometimes needs the link.
- Used in both light and dark Windows themes.

## Capabilities and Constraints

- Read-only Modbus over BLE; never writes station registers.
- Station telemetry: battery %, AC/DC in/out power, AC/DC input voltage, output on/off, model, serial, firmware, and settings (ECO, charging mode, power lifting, LED mode).
- Only aggregate AC and DC power is reported; there is no per-port power.
- The owner's unit is the 230 V EB3A with one AC socket.
- Phase 2 candidates, not built: automatic PC shutdown/hibernate on low battery, other Bluetti models.

## Brand Commitments

- Name: `upsy` (confirmed).
- A mascot character in a flat, modern mascot-logo style with a mid-century palette (owner's brief), in the spirit of the owner's Buryachenko beet mascot.
- In-app UI follows native Windows 11 conventions; brand lives in the mascot, accent colour and small details, not in custom chrome.

## Evidence on Hand

- Real telemetry captures in `local/watch1.log`, `local/watch2.log`, `local/dump1.log` (gitignored; values are copied into unit tests).
- No product photos are used; the station glyph is our own drawing.

## Product Principles

1. Glanceable first: the tray icon alone answers "am I OK?".
2. Never cry wolf: no alert from stale data, glitches or the state found at start-up.
3. Honest numbers: runtime is labelled approximate; unknown is shown as unknown.
4. Native, quiet, small: feels like part of Windows, costs little memory and attention.

## Accessibility & Inclusion

Text contrast ≥ 4.5:1 in both themes; state never conveyed by colour alone (glyph + text); full keyboard support in the flyout (Tab, Enter, Escape); respects Windows text scaling and DPI.
