# upsy

Windows tray app that turns portable power stations into UPS monitors: it reads them over Bluetooth LE and shows battery, runtime, charging and mains state, with alerts. Supported: Bluetti EB3A and EcoFlow DELTA 2, several at once.

- Product truth: `PRODUCT.md`. Visual decisions and tokens: `DESIGN.md`. Mock-ups: `design/*.html`.
- Planned and finished work: `openspec/` (specs in `openspec/specs`, changes in `openspec/changes`, archive under `changes/archive`). Project rules for proposals, designs, specs and tasks are in `openspec/config.yaml`; read it before planning.
- User docs: `README.md` (screenshots in `docs/images`, regenerated with `--render`).

## Layout

| Path | What |
|---|---|
| `src/Upsy.Core` | Pure logic. `Link`: GATT channel, driver interface, `StationMonitor` (radio, watchdog, backoff, pause). `Station`: models, `StationCatalog` (profiles, advertisement recognition), `StationRegistry`, `BluettiDriver`, EB3A registers. `Protocol`: Modbus and `Protocol/EcoFlow` (frames, crypto, session, `Delta2Decoder`, account login). `Settings` (v2, migration, protected user ID), `Power`, `Alerts`, `Presentation`. No Windows UI references. |
| `src/Upsy` | Composition root (`Program.cs`, `Tray/TrayApp.cs`: one monitor per station), WinRT transport and the single `AdvertisementHub` (`Ble`), tray icon and menus (`Tray`), WPF flyout with one `StationView` per station and generated glyphs (`Flyout`, `Flyout/Glyphs`), sign-in / Add station / confirm windows (`Dialogs`), settings store, DPAPI, key table, autostart, theme (`Infrastructure`). |
| `tests/Upsy.Core.Tests` | xUnit v3 tests for Core: captured EB3A registers and DELTA 2 heartbeats, a fake GATT channel, and a fake DELTA 2 that runs the station side of the handshake. |
| `assets/` | Brand (mascot, app icon) and station drawings. `assets/station/*-outline.svg` are the owner's drawings; `*.svg` and `src/Upsy/Flyout/Glyphs/*Glyph.xaml` are generated from them. |
| `tools/` | `gen_station_glyph.py` (glyph generator), `install.ps1` (per-user install), `get-ecoflow-keydata.ps1` (EcoFlow key table, pinned and hash-checked). |
| `local/` | Untracked: BLE spikes (`local/spike` for Bluetti, `local/efspike` for EcoFlow login/handshake/capture), captures, and `local/devices.md` with the owner's real device identifiers. |

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
- Generated files (`assets/station/{eb3a,delta2}.svg`, `src/Upsy/Flyout/Glyphs/*Glyph.xaml(.cs)`) are edited through their generator, not by hand.
- No PII in tracked files, designs or screenshots: use placeholder identifiers in the real format (EB3A `2240000000001`, DELTA 2 `R331EXAMPLE00001` / `R33-0001`, address `A1B2C3D4E5F6`). Real ones stay in `local/devices.md`; captured fixtures get their serial swapped.
- Never commit EcoFlow's key table, an EcoFlow user ID or credentials.
- Hardware checks are separate tasks from unit-tested ones; mark them done only after they were run on the real device.

## Hardware notes

- EB3A: BLE name `EB3A…`, service `ff00` (notify `ff01`, write `ff02`), Modbus RTU function 0x03 over BLE, no pairing. Registers are listed in README "Registers used". The station allows one BLE client: the Bluetti phone app and upsy cannot be connected at the same time (use Pause).
- EcoFlow DELTA 2: advertisement manufacturer data `0xB5B5` (serial, battery % at byte 17, encryption type 7), service `0001` (write `0002`, notify `0003`). Session: secp160r1 ECDH (use CNG explicit parameters; named-curve export pads to 21 bytes), session key from the key table, auth with md5(userId+serial); auth reply `06` means the station is bound to another account (remove and re-add it in the EcoFlow app). After a link ends it refuses service discovery for 15-20 s. Heartbeat fields are in README.
- The WPF tray glyphs need DirectWrite: GDI+ drops Segoe Fluent Icons at 20 and 24 px.
