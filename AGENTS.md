# upsy

Windows tray app that turns a portable power station into a UPS monitor: it reads the station over Bluetooth LE and shows battery, runtime, charging and mains state, with alerts. Supported: Bluetti EB3A.

- Product truth: `PRODUCT.md`. Visual decisions and tokens: `DESIGN.md`. Mock-ups: `design/*.html`.
- Planned and finished work: `openspec/` (specs in `openspec/specs`, changes in `openspec/changes`, archive under `changes/archive`). Project rules for proposals, designs, specs and tasks are in `openspec/config.yaml`; read it before planning.
- User docs: `README.md` (screenshots in `docs/images`, regenerated with `--render`).

## Layout

| Path | What |
|---|---|
| `src/Upsy.Core` | Pure logic: protocol framing and decoding (`Protocol`, `Station`), link state machine (`Link`), power state and runtime (`Power`), alerts, view models (`Presentation`). No Windows UI references. |
| `src/Upsy` | Composition root (`Program.cs`), WinRT BLE transport (`Ble`), tray icon and menu (`Tray`), WPF flyout (`Flyout`), settings, autostart and theme (`Infrastructure`). |
| `tests/Upsy.Core.Tests` | xUnit v3 tests for Core, including frames captured from a real station. |
| `assets/` | Brand (mascot, app icon) and station drawings. `assets/station/eb3a-outline.svg` is the owner's drawing; `eb3a.svg` is generated from it. |
| `tools/` | `gen_station_glyph.py` (station glyph generator), `install.ps1` (per-user install). |
| `local/` | Untracked scratch: BLE spike (`local/spike`, scan/probe/listen tools) and capture logs. |

## Commands

```powershell
dotnet build upsy.slnx -c Release                      # must be 0 warnings (warnings are errors)
dotnet test --solution upsy.slnx -c Release            # Microsoft.Testing.Platform (global.json)
dotnet format upsy.slnx --verify-no-changes
openspec validate <change> --strict

src/Upsy/bin/Release/net10.0-windows10.0.19041.0/upsy.exe --render <dir>   # every tray/flyout/menu state to PNG
python tools/gen_station_glyph.py                      # after editing the station glyph script
pwsh tools/install.ps1                                 # install/update to %LOCALAPPDATA%\Programs\upsy
```

The quality gate (build, test, format) must pass before a task is marked done. Only one instance runs (named mutex); stop the installed `upsy.exe` before running a dev build against the same station. Runtime log: `%LOCALAPPDATA%\upsy\upsy.log`; settings: `%APPDATA%\upsy\settings.json`.

## Conventions

- Code, docs, specs and commit messages in English. Conventional Commits, split by concern.
- Work goes through OpenSpec: propose, then design mock-up first (`design/upsy.design.html`, light and dark, reviewed with impeccable and /ux-crux-review), then apply. The WPF UI follows the accepted mock-up; change the mock-up before changing the UI.
- Read-only towards the station: never write registers or send control commands.
- Pure logic takes plain values and an injected `TimeProvider`; no hardware in unit tests. Use frames captured from the real device as test data.
- Generated files (`assets/station/eb3a.svg`, the `station-glyph` block in `FlyoutWindow.xaml`) are edited through their generator, not by hand.
- Hardware checks are separate tasks from unit-tested ones; mark them done only after they were run on the real device.

## Hardware notes

- EB3A: BLE name `EB3A…`, service `ff00` (notify `ff01`, write `ff02`), Modbus RTU function 0x03 over BLE, no pairing. Registers are listed in README "Registers used". The station allows one BLE client: the Bluetti phone app and upsy cannot be connected at the same time (use Pause).
- The WPF tray glyphs need DirectWrite: GDI+ drops Segoe Fluent Icons at 20 and 24 px.
