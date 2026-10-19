# Tasks

## 0. Design (done before apply)

- [x] 0.1 Mock every surface in `design/upsy.design.html` (tray states, tooltip, flyout states, menu, toasts, brand) in light and dark; verify by rendering both themes and reviewing the screenshots
- [x] 0.2 Draw the brand (`assets/brand/upsy-mascot.svg`, `upsy-icon.svg`) and the station glyph (`assets/station/eb3a.svg`); verify at 16-256 px
- [x] 0.3 Review with the impeccable detector and `/ux-crux-review`, fix the findings, record DESIGN.md and PRODUCT.md; verify the detector returns no findings

## 1. Project setup

- [x] 1.1 Create `upsy.slnx` with `src/Upsy.Core`, `src/Upsy` (WinExe, `UseWindowsForms` + `UseWPF`, `net10.0-windows10.0.19041.0`) and `tests/Upsy.Core.Tests` (xUnit v3); verify `dotnet build` and `dotnet test` succeed
- [x] 1.2 Add `global.json`, `Directory.Build.props` (nullable, warnings as errors, latest-recommended analyzers, code style in build), `Directory.Packages.props`, `.editorconfig`; verify a Release build has 0 warnings and `dotnet format --verify-no-changes` passes
- [x] 1.3 Add the GitHub Actions workflow (windows-latest: restore, build, test, format check); verify the YAML runs the same commands as the local quality gate

## 2. Protocol and decoding

- [x] 2.1 Implement Modbus CRC16 and the read-registers frame builder; verify unit tests against the frame `0103000A002865D6` captured in the spike
- [x] 2.2 Implement frame reassembly from notification chunks and the response validator (length, function code, exception responses, CRC); verify tests for split frames, bad CRC and short frames
- [x] 2.3 Implement the register decoder to a `Telemetry` record (battery %, powers, AC/DC input voltages, output flags) and `StationIdentity` (model, serial, ARM/DSP firmware); verify tests using the live dump (model `EB3A`, serial `2240000000001`, ARM 2063.02, DSP 2052.07) and the spike values (battery 54, AC out 126 and AC in 390 at 228.4 V)
- [x] 2.4 Implement the settings decoder (3034 LED mode, 3063 ECO, 3064 ECO hours, 3065 charging mode, 3066 power lifting); verify tests with the dump (LED off, ECO on 1 h, Standard, power lifting off)

## 3. Station link

- [x] 3.1 Implement scanning by `EB3A` advertising name and connecting by cached address; verify against the real EB3A that it is found without pairing
- [x] 3.2 Implement characteristic setup and request/response with a 4 s timeout; verify a live read returns a valid snapshot
- [x] 3.3 Implement the 2 s polling loop, identity decode on the first cycle, and the settings read once per connection and then every minute; verify telemetry updates for several minutes
- [ ] 3.4 Implement reconnection with backoff up to 30 s, retry of transient connect failures, Bluetooth radio monitoring and reconnect on resume from sleep; verify by toggling the PC's Bluetooth and the app recovers without restart (station-side and sleep checks are in 7.2) (implemented and unit-tested with a fake transport; live Bluetooth-toggle check pending)
- [ ] 3.5 Implement Pause for 30 minutes / Resume with automatic resume; verify unit tests with a fake clock and a live pause/resume (implemented and unit-tested with a fake clock; live phone-app handover pending)

## 4. Power state logic

- [x] 4.1 Implement on-mains (voltage-based), net battery power, charging and load-present classification; verify unit tests with the unplugged (0 W, 2.6 V), plugged (390 W, 228 V), full-and-idle (0 W, 228 V) and solar-on-battery cases
- [x] 4.2 Implement the discharge-power moving average and runtime estimate with the 10 s warm-up, reset on reconnect and the 24 h cap, plus the rounded runtime text; verify tests for fluctuating load, no load, charging, on mains and rounding
- [x] 4.3 Implement alert edge detection (low threshold and critical 10% with re-arm, already-low at start, 5 s debounced power loss/restore, no transition at connect, silence while disconnected or paused) with an injected clock; verify unit tests for each scenario

## 5. Tray UI

- [x] 5.1 Implement the tray icon renderer (digits + state bar, full glyph, disconnected and paused glyphs, theme-aware, DPI-sized, old handles released); verify by rendering every state to PNG at 16 and 32 px in both themes
- [x] 5.2 Implement the tooltip text builder with the 127-character cap; verify unit tests for on-battery, charging, on-mains and each disconnected reason
- [x] 5.3 Build the station glyph in XAML from `assets/station/eb3a.svg` with bindable parts, plus the generic fallback; verify it renders in the flyout at 100% and 200% scaling
- [ ] 5.4 Implement the WPF flyout per DESIGN.md (theme, rounded corners, anchored to the taskbar, glyph, main block, In/Out strip, Details expander, no-data notices, footer); verify it opens near the tray, follows live data, and closes on outside click or Escape (implemented and rendered for every state via `upsy --render`; live outside-click and anchoring check pending)
- [ ] 5.5 Implement the right-click menu (Open upsy, Pause/Resume, Start with Windows, low threshold, Exit) with JSON settings and the `HKCU\...\Run` value, and keyboard activation of the icon; verify settings survive a restart and Exit removes the icon (implemented; the remembered address survives a restart, live check of menu settings pending)
- [x] 5.6 Enforce a single instance with a named mutex and add rotating error logging to `%LOCALAPPDATA%\upsy`; verify a second launch exits without a second icon

## 6. Notifications

- [ ] 6.1 Show mains lost, mains back, battery low and battery critical notifications via the tray balloon/toast with the DESIGN.md copy; verify each text with unit tests on the message builder and one live toast (message builder unit-tested; live toast check pending)

## 7. Docs and end-to-end validation

- [x] 7.1 Write README.md (what, screenshots, install/run, how it works, registers, development, troubleshooting, roadmap); verify every command in it runs
- [ ] 7.2 Owner: run against the real EB3A, unplug and replug mains, toggle AC/DC outputs, sleep the PC, switch the station's Bluetooth off and on; verify icon, tooltip, flyout and notifications follow the state changes (mains unplug/replug done twice on 2026-10-12, 100% to 92% over 9 min, MainsLost and MainsRestored alerts raised each time per upsy.log; output toggles, sleep and station Bluetooth toggle not yet run)
- [ ] 7.3 Owner: leave the app running for several hours on battery and on mains, verify the link stays up or recovers, log a discharge to calibrate the `usable` factor, and record phone-app coexistence and Do Not Disturb behaviour in the README
