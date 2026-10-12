# Proposal

## Why

The Bluetti EB3A is used as a UPS for a Windows PC, and its charge level and remaining runtime are visible only on the device itself. There is no official Windows app and no existing tray tool for Bluetti. A spike confirmed the station can be read over BLE from Windows with no pairing, PIN, or encryption, so a small native tray app (`upsy`) can show battery, runtime and charging state, and warn before the UPS runs out.

## What Changes

- New .NET 10 LTS Windows tray app (`upsy`) using WinRT Bluetooth LE directly, read-only.
- Design first: an HTML/CSS mock-up of every surface (`design/upsy.design.html`, light and dark, real Windows 11 sizes), reviewed with the impeccable skill and `/ux-crux-review`, plus a brand (mascot, app icon, wordmark) recorded in DESIGN.md.
- Discover the EB3A by its `EB3A<serial>` advertising name, connect over GATT (service `0000ff00`), and poll Modbus registers about every 2 s through `0000ff02` (write) / `0000ff01` (notify).
- Reconnect automatically when the link drops, after PC sleep, or when the Bluetooth radio comes back; show why it is disconnected. Allow pausing the connection for 30 minutes so the Bluetti phone app can use the station.
- Derive state the station does not report: on mains vs on battery, charging (including DC/solar input), load present, and remaining runtime (capacity x battery % / smoothed discharge power).
- Tray icon in the native Windows 11 style, with tooltip and context menu.
- Left-click flyout with a small (96x72) vector glyph of the recognised station model whose battery fill, outputs and inputs light up from live data, plus expandable details including the station's settings (ECO, charging mode, power lifting, light).
- Windows notifications for low charge, critical charge (10%), and power-loss / mains-restored transitions.
- Out of scope for phase 1: other Bluetti models and EcoFlow, controlling the station (writing registers 3000+), MQTT / Home Assistant.
- Later (potential features): automatic PC hibernate/shutdown at critical charge; other Bluetti models; an installer/MSIX package.

## Capabilities

### New Capabilities
- `station-link`: Discovering, connecting to and polling the EB3A over BLE, parsing registers into a telemetry snapshot, and reconnecting after drops, sleep and radio changes.
- `power-state`: Deriving on-mains / charging / load state and smoothed remaining runtime from telemetry.
- `tray-ui`: Tray icon, hover tooltip, left-click flyout with station glyph and details, and right-click menu with settings.
- `low-battery-alerts`: Notifications for low and critical charge and for power-loss / mains-restored transitions.

### Modified Capabilities
<!-- None: no specs exist yet. -->

## Impact

- New code: a .NET 10 (`net10.0-windows10.0.19041.0`) solution: `Upsy.Core` (pure logic), `Upsy` (WinForms + WPF app) and `Upsy.Core.Tests`; shared build settings, central package management, analyzers and CI. No existing code is affected.
- Assets: our own `assets/station/eb3a.svg` station glyph (not a Bluetti photo), the brand set in `assets/brand/`, and the design mock-up in `design/`.
- Dependencies: Windows Bluetooth LE (WinRT), WinForms and WPF from the Windows Desktop runtime; no third-party BLE, UI or SVG library.
- Risks to validate: stability of a connection held for hours, EB3A turning BLE off after inactivity, coexistence with the Bluetti phone app, and notifications hidden by Do Not Disturb.
- Reference only: `local/spike` (gitignored) holds the throwaway spike.
