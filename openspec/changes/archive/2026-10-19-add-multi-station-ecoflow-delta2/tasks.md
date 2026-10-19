# Tasks

## 0. Design (before any UI code)

- [x] 0.1 Owner: draw the DELTA 2 outline as `assets/station/delta2-outline.svg`; delivered 2026-10-19 (front view: shell, handle, screen, 4 USB-A + 2 USB-C panel, power button; AC, 12 V and inputs are at the back)
- [x] 0.2 Add frames to `design/upsy.design.html`, light and dark:
  - `#multi` (EB3A + DELTA 2 flyout, tray station first, per-station ⋯);
  - `#adv-only` (battery level only, for each reason: sign-in needed, key rejected, key table missing, phone holds the link);
  - `#signin`;
  - `#add-station` (with stations, empty, searching);
  - `#menu-multi` (Show in tray, Add station, Sign out);
  - `#glyph-delta2` (generated from the outline: screen gauge and USB group live);
  - a tooltip with two stations.

  Verify by rendering both themes with the screenshot helper and checking real Windows 11 sizes.
- [x] 0.3 Review the new frames with the impeccable detector and `/ux-crux-review`, fix the findings, and record the new surfaces and tokens in DESIGN.md; verify the detector returns `[]`
- [x] 0.4 Owner accepts the mock-up; verify by recording the acceptance date in DESIGN.md

## 1. Core: driver boundary (no behaviour change for the EB3A)

- [x] 1.1 Add `IGattChannel` and `IAdvertisementSource` to Core (open by address and UUIDs, write with or without response, notification stream, advertisement stream); verify a fake channel in tests can script notifications
- [x] 1.2 Add `IStationDriver` and `StationUpdate` (telemetry, identity, settings, failure reason), plus `StationProfile` (model name, nominal Wh, glyph key, minimum reconnect delay); verify with compile-only tests of the EB3A profile values (268 Wh)
- [x] 1.3 Move the EB3A poll loop and `Eb3aRegisters` decoding into a Bluetti driver running on `IGattChannel` (Modbus over `ff00`/`ff01`/`ff02`); verify the existing decoding tests pass and a new driver test with a fake channel replays the captured EB3A frames
- [x] 1.4 Refactor `StationMonitor` to run any driver: keep radio, backoff, pause, sleep/resume, and add the 10 s no-update watchdog, the per-profile minimum reconnect delay, and the "service missing" retry; verify the existing monitor tests pass and new tests cover the watchdog and the 5 s minimum delay with a fake clock
- [x] 1.5 Generalise the models: `Telemetry.ToEmpty`/`ToFull`, `StationIdentity` with a firmware list, settings as ordered label/value readouts (EB3A: LED, ECO, ECO hours, charging mode, power lifting); verify the EB3A readout test reads LED off, ECO on 1 h, Standard, power lifting off
- [x] 1.6 `PowerState` uses the profile capacity and a station-reported `ToEmpty`/`ToFull`; verify tests: DELTA 2 431 min → "about 7 h 10 min", 73 min to full → "about 1 h 10 min", EB3A estimate unchanged

## 2. Core: EcoFlow protocol

- [x] 2.1 Implement CRC8 (poly 0x07) and CRC16/ARC, the v2 inner packet codec with the `seq[0]` payload XOR, and the `5A5A` outer frame with reassembly across notifications; verify tests with frames from `local/ecoflow/captures` (auth status reply `0100`, auth ok `00`, a split frame, a bad CRC)
- [x] 2.2 Implement the crypto: secp160r1 ECDH via explicit parameters (20-byte coordinates), key/iv from the shared secret, session key from the key table and `seed`/`srand`, AES-CBC send and receive; verify tests: a known-answer test with fixed keys, the CNG 21-byte padding case, and the seed → offset rule (`0302` → 304, `0102` → 272)
- [x] 2.3 Implement the handshake as a state machine on `IGattChannel` (public key, session key, auth status, auth; `00` ok, `06` wrong key, other codes as errors, timeouts); verify with a fake channel acting as the station (scripted ECDH peer) for success, wrong key and timeout
- [x] 2.4 Implement the DELTA 2 decoders (PD, EMS, INV, MPPT) per design decision 4 and the snapshot fold that publishes after PD, EMS and INV; verify with captured payloads: on battery 82%, 103 W AC out, 431 min; on mains 80%, 328 W at 233.3 V, 73 min to full; USB and 12 V on with 0 W
- [x] 2.5 Implement the DELTA 2 advertisement parser (serial, battery %, encryption type) and recognition (`0xB5B5`, `R331`/`R335`); verify tests with the captured ad (`R331EXAMPLE00001`, 0x54 → 84%, type 7) and an unsupported serial
- [x] 2.6 Implement the EcoFlow driver (handshake → heartbeats → updates, identity from ad and PD firmware, settings readouts); verify a driver test from handshake to the first full snapshot with a fake channel

## 3. Registry, settings and secrets

- [x] 3.1 Implement `StationRegistry` (add, forget, display names with serial suffix for duplicate models, tray station with fallback); verify tests for each `station-registry` scenario
- [x] 3.2 Implement settings v2 and migration from v1 (`StationAddress` → one EB3A entry, backup to `settings.v1.json`); verify tests with a v1 file and a round trip of v2
- [x] 3.3 Store the EcoFlow user ID with DPAPI (`CurrentUser` plus entropy) and keep it out of logs; verify a test that the settings file holds no plain user ID and that sign-out removes it
- [x] 3.4 Implement the key table loader (`%LOCALAPPDATA%\upsy\ecoflow-keydata.bin`, 65,280 bytes, SHA-256 check) and `tools/get-ecoflow-keydata.ps1` (pinned ha-ef-ble commit, hash check); verify the script produces the file with the expected hash and the loader rejects a truncated copy

## 4. App: links and account

- [x] 4.1 Implement the WinRT `IGattChannel` (service lookup, write with response, notifications, connection status) and a single shared `AdvertisementHub`; verify the EB3A connects live through the new channel
- [x] 4.2 Compose one monitor per registry entry in `TrayApp`, with first-station auto-add, address refresh from the hub, and add/forget at runtime; verify live: the EB3A and the DELTA 2 both report data, and forgetting one releases its link (2026-10-19: EB3A and DELTA 2 live together; Forget confirmation checked in renders)
- [x] 4.3 Implement the EcoFlow login client (`api.ecoflow.com`, fallback `api-e`, error messages); verify a unit test with a fake HTTP handler for success, wrong password and network failure
- [x] 4.4 Implement the advertisement-only state for EcoFlow stations (sign-in needed, key table missing, key rejected with Try again, link held elsewhere); verify a monitor test per reason and that no alerts are raised from it

## 5. App: UI (follows the accepted mock-up)

- [x] 5.1 Generalise `tools/gen_station_glyph.py` to one shape list per model, writing `assets/station/<model>.svg` and `Flyout/Glyphs/<Model>Glyph.xaml` with standard part names; regenerate the EB3A and add the DELTA 2 from `delta2-outline.svg` (live parts: screen gauge, USB-A/USB-C group as DC out); verify the EB3A render is pixel-identical to before and the DELTA 2 glyph reads clearly at 72×96 in both themes
- [x] 5.2 Extract the station section of the flyout into a `StationView` and stack one per station, tray station first, per `#multi`; verify `upsy --render` outputs single- and two-station flyouts in both themes that match the frames
- [x] 5.3 Tray icon and tooltip follow the tray station, with other stations appended to the tooltip within 127 characters; verify unit tests on the tooltip builder (single, two stations, truncation) and a render of each tray state
- [x] 5.4 Context menu: Pause/Resume for the tray station, Show in tray (2+ stations), Add station, Sign out of EcoFlow, plus per-station ⋯ in the flyout (Pause/Resume, Show in tray, Forget); verify renders against `#menu-multi` and a live switch of the tray station that survives a restart (2026-10-19: owner switched the tray to DELTA 2; settings.json keeps it as TrayStationId, which a restart loads)
- [x] 5.5 Sign-in window per `#signin` (email, password, progress, EcoFlow error, cancel); verify renders in both themes and a live sign-in that brings the DELTA 2 from battery level only to live data (2026-10-19: owner signed in; DELTA 2 went from SignInNeeded to live data in 2 s)
- [x] 5.6 Add station window per `#add-station`, fed by the hub; verify live: the DELTA 2 is listed when not added, and adding it shows it in the flyout (2026-10-19: owner added the DELTA 2 from the window)
- [x] 5.7 Per-station notifications titled with the station name; verify message-builder tests for EB3A and DELTA 2 copy

## 6. Docs and tooling

- [x] 6.1 `install.ps1` fetches the key table when missing; verify a fresh install on this PC ends with the key table present and both stations connected (2026-10-19: install.ps1 found the key table, installed copy runs both stations)
- [x] 6.2 Add `THIRD-PARTY-NOTICES.md` (ha-ef-ble, Apache-2.0) and source headers on ported EcoFlow files; verify every EcoFlow protocol file has the header
- [x] 6.3 Update README (supported stations, EcoFlow sign-in and key table, rebind tip for wrong key, DELTA 2 fields, screenshots), CLAUDE.md (layout, EcoFlow notes) and DESIGN.md; verify the README commands run
- [x] 6.4 Run the quality gate (Release build with 0 warnings, tests, format check, `openspec validate --strict`); verify all pass

## 7. Hardware checks (owner, real stations)

- [x] 7.1 Upgrade the installed copy: verify the EB3A carries over from v1 settings without a new scan, and threshold and autostart are unchanged (2026-10-19: settings migrated to v2 with the EB3A as tray station, 20% kept, settings.v1.json kept, EB3A reconnected by address; reinstalled copy connected too)
- [x] 7.2 DELTA 2 on mains: unplug for more than 5 s and plug back; verify the mains lost and mains restored notifications name DELTA 2 and the EB3A is unaffected (2026-10-19: MainsLost and MainsRestored for DELTA 2, EB3A unaffected)
- [x] 7.3 DELTA 2 DC load: plug a phone into USB and a 12 V device into the car port; verify DC output watts and flags match the station screen (confirms the port-watt offsets) (2026-10-19: owner confirmed USB and 12 V watts against the screen)
- [x] 7.4 Pause the DELTA 2, open the EcoFlow app on the phone, then resume; verify the hand-over works while the EB3A stays connected, and the advertised level shows while paused if the station still advertises (2026-10-19: paused 17:10:01, resumed and reconnected 17:10:41)
- [x] 7.5 Run both stations for several hours including a PC sleep; verify both links stay up or recover, and record the results in the README (accepted by the owner on 2026-10-19 without the multi-hour run; README Status still lists it as not run)
