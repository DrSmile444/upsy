# Design

## Context

How upsy works today (see the archived `2026-10-19-add-eb3a-tray-monitor`):

- `StationMonitor` (Core) owns the link lifecycle: radio check, connect, poll, backoff, pause. It calls `IStationSession.ReadAsync(start, count)`, which only fits Modbus, and decodes through `Eb3aRegisters`.
- `WinRtStationTransport` (app) scans for names starting with `EB3A` and opens service `ff00`.
- `Telemetry`, `StationIdentity` (ARM/DSP firmware) and `StationSettings` (LED, ECO, charging mode, power lifting) are EB3A-shaped.
- `AppSettings` holds one `StationAddress`. `TrayApp` wires one monitor to one icon and one flyout.
- The station glyph is generated into `FlyoutWindow.xaml` between markers.

### What the spike established

The owner's DELTA 2 was read end to end on 2026-10-19 with `local/efspike`. Captures are in `local/ecoflow/captures`. Serial numbers, names and addresses in this change are placeholders in the real format; the owner's identifiers stay in `local/`.

**Advertisement**
- Manufacturer data `0xB5B5`. Layout: `[0]` 0x13, `[1..16]` ASCII serial `R331EXAMPLE00001`, `[17]` battery % (read 0x54 = 84 and 0x57 = 87, both matching the screen), `[22]` flags 0x3E, i.e. encryption type 7.
- Scan response: name `R33-0001`, plus `0xC5C5` data of unknown meaning.
- GATT service `00000001-…`: write `…0002`, notify `…0003`. MTU 500.

**Session (encryption type 7)**
- Outer frame: `5A 5A | type<<4 | 01 | len LE | payload | CRC16`.
- Steps:
  1. Plain `01 00 + X‖Y` with secp160r1 coordinates, 20 bytes each. The station replies `01 00 00 + X‖Y`.
  2. `key = shared[0..16]`, `iv = md5(shared)`.
  3. Plain `02`. The reply is encrypted and carries `srand[16]` and `seed[2]`. The session key is `md5(keydata[seed[0]*16 + ((seed[1]-1)&0xFF)*256 .. +16] ‖ srand)`.
  4. Encrypted inner packets follow: auth status `35/89`, then auth `35/86` with the upper-case hex of `md5(userId + serial)`. The reply `00` means ok, `06` means wrong key.
- Inner packet v2: `AA 02 len CRC8 0D seq[4] 00 00 src dst cmdSet cmdId payload CRC16`. The payload is XORed with `seq[0]` when that byte is non-zero.
- Getting `06` was fixed by removing and re-adding the station in the EcoFlow app. The user ID is the same on every EcoFlow API region.

**Data flow**
- After auth the station pushes about 7 packets/s with no acknowledgements: PD `02:20/02` (133 B), EMS `03:20/02`, INV `04:20/02`, MPPT `05:20/02`, BMS `03:20/32`, kit `03:03/0E`.
- 5 minutes without a drop, while the installed upsy held the EB3A at the same time.
- After a link ends, the station returns an empty service list for 15–20 s.

**Fields verified against the screen and toggles**
- PD: SOC @14, total out @15, total in @17, signed remain minutes @19, USB state @24, 12 V state @33, AC in W @112, AC out W @114.
- EMS: chg/dsg remain minutes @17/@21 (5939 = n/a), f32 SOC @26, max charge SOC @12.
- INV: AC in voltage mV @23, AC out voltage @14, AC enabled @45.
- Readings: on battery 82%, 103 W out, 431 min to empty. On mains 80%, 328 W in, 233.3 V, 73 min to full.
- EMS `chgState` is 1 both on and off mains, so it cannot detect mains.
- PD `@118` read 118 W on mains with nothing plugged into DC, so it is not DC output.

**Platform**
- Windows CNG supports secp160r1, but exporting with a named curve pads coordinates to 21 bytes with a trailing zero. `ExportExplicitParameters` gives the correct 20 bytes.

## Goals / Non-Goals

**Goals:**
- One model-agnostic monitor, alert and UI pipeline; a model is a driver plus a glyph plus a capacity.
- EcoFlow protocol code in Core, pure and unit-tested with captured frames. The WinRT layer only moves bytes.
- No regression for a single-EB3A user: same first start, same tray and flyout when only one station is listed.

**Non-Goals:**
- Any write that changes station state (no control).
- EcoFlow encryption types 0/1, protocol v3/v4 and other EcoFlow models. They stay possible but are not built or tested.
- One tray icon per station.
- Cloud data.

## Decisions

1. **Drivers produce updates; the monitor owns the lifecycle.** `IStationDriver` (Core) is one per model.
   - **Identify:** recognises an advertisement and returns model, serial, name and advertised level.
   - **Run:** given an open GATT channel, `RunAsync(channel, ct)` yields `StationUpdate`s: telemetry, identity, settings, or a failure reason.
   - **Bluetti driver:** wraps today's poll loop and `Eb3aRegisters`.
   - **EcoFlow driver:** runs the handshake, then folds pushed packets into a snapshot. It publishes once PD, EMS and INV have each arrived since the connection opened.
   - **Monitor:** keeps radio, backoff, pause, sleep/resume and the 10 s "no update" watchdog. It gains a per-driver minimum reconnect delay (5 s for EcoFlow) and retries "service missing".
   - Rejected: a `ReadAsync`-style session for both vendors, because EcoFlow is push-only and session-stateful. Also rejected: one monitor class per vendor, because it duplicates the backoff and pause logic that is already tested.

2. **Byte channel boundary.**
   - Core defines `IGattChannel`: open by address with service, write and notify UUIDs; `WriteAsync(bytes, withResponse)`; a notification stream; `IsConnected`.
   - Core also defines `IAdvertisementSource`: a stream of address, name, manufacturer data and RSSI.
   - The app implements both with WinRT.
   - Drivers and the handshake are tested with a fake channel that replays captured frames.
   - Rejected: drivers calling WinRT directly. That makes the protocol untestable without hardware.

3. **Generic telemetry, labelled settings.**
   - `Telemetry` keeps its current fields: percent, DC/AC in/out W, AC/DC in V, AC/DC out on. It gains `TimeSpan? ToEmpty` and `TimeSpan? ToFull`, both reported by the station and null for the EB3A.
   - `StationIdentity` becomes model, serial and a list of `(label, version)` firmware entries. EB3A: ARM and DSP. DELTA 2: the PD system version.
   - Settings become an ordered list of `(label, value)` readouts that each driver formats, so the flyout renders any model without a per-model switch.
   - A `StationProfile` per model holds the display model name, nominal Wh (268 and 1024), glyph key, driver and minimum reconnect delay.
   - Rejected: typed settings records per vendor, because every new model would mean UI code.

4. **DELTA 2 mapping.**
   - Percent: PD SOC.
   - AC in W and V: PD @112 and INV @23 ÷ 1000. Mains uses the existing rule, AC in voltage above 100 V.
   - DC in W: PD `dc_pv_input_watts`.
   - AC out W: PD @114.
   - DC out W: the sum of PD USB, QC, USB-C and 12 V port watts.
   - AC out on: INV @45. DC out on: USB state @24 or 12 V state @33.
   - To empty / to full: EMS @21 / @17, ignored when 5939 or when they disagree with the current direction.
   - Settings: charge limit (EMS max charge SOC), discharge limit (EMS min discharge SOC), X-Boost (INV), AC charging power (MPPT).
   - Offsets come from ha-ef-ble struct definitions plus the spike. The ones not yet verified on hardware (port watts, MPPT, X-Boost) are checked against captures in tasks before relying on them.

5. **Runtime.** `PowerState` uses `ToEmpty` / `ToFull` when present. Otherwise it keeps the smoothed estimate with the profile's capacity instead of the EB3A constant.

6. **One shared scanner.**
   - An `AdvertisementHub` runs a single active WinRT watcher for the whole app.
   - It feeds: the first-station auto-add, the Add station list, each listed station's address refresh, and the advertised battery level for EcoFlow stations without a session.
   - Rejected: a watcher per monitor. That wastes radio time, and WinRT watchers compete.

7. **Registry and settings v2.**
   - `settings.json` holds:
     - `Version: 2`;
     - `Stations: [{ Id, Model, Address, Serial, Name? }]`;
     - `TrayStationId`;
     - `LowPercent`;
     - `EcoFlowUserId` (DPAPI-protected base64).
   - `Id` is the serial when known, else the address.
   - Loading a v1 file (`StationAddress`) creates one EB3A entry and saves v2. The old file is kept as `settings.v1.json`.
   - `StationRegistry` (Core) is the list plus events. `TrayApp` creates and disposes one `StationMonitor` per entry.

8. **EcoFlow account.**
   - Sign-in posts the ha-ef-ble payload to `https://api.ecoflow.com/auth/login` (`scene IOT_APP`, base64 password, `userType ECOFLOW`) and reads `data.user.userId`. It tries `api-e` only if `api` fails, because the spike got the same ID from every region.
   - The user ID is stored with `ProtectedData.Protect(…, CurrentUser)` and app-specific entropy. The password exists only in the sign-in window's `PasswordBox` and the request.
   - One account serves every EcoFlow station.
   - Rejected: asking the user to paste a user ID, because the EcoFlow app does not show it.

9. **Key table outside git.**
   - The app loads `%LOCALAPPDATA%\upsy\ecoflow-keydata.bin` and checks it is 65,280 bytes with SHA-256 `3d1ba59b40c46bc86ba35fa231a3db196b448bbdfb1635c88ddd42f24abd4b2a`.
   - `tools/get-ecoflow-keydata.ps1` downloads `keydata.py` from rabits/ha-ef-ble at commit `1c1eb3310eb1ec1cee9cd4eab5f28a1f0f7aba83`, decodes the base64 and verifies the hash before writing.
   - `install.ps1` runs it when the file is missing.
   - Rejected: embedding it in the repo (the owner's rule, and it is EcoFlow's), or the app downloading it on its own (silent network access for protocol data).

10. **Crypto with in-box .NET.**
    - `ECDiffieHellman` on secp160r1. Exports use explicit parameters; device keys are imported with the explicit curve.
    - `DeriveRawSecretAgreement` (20 bytes); `Aes` CBC (PKCS7 on send, block-aligned with lenient unpad on receive); `MD5`.
    - CRC8 (poly 0x07) and CRC16/ARC are hand-written like the existing Modbus CRC.
    - Rejected: BouncyCastle, which is not needed once the CNG padding quirk is known.

11. **UI.** Frames are added to `design/upsy.design.html` before any WPF work. The owner reviews them with impeccable and /ux-crux-review.
    - **Multi-station flyout** (`#multi`): stacked station sections, tray station first, each with glyph, percentage, status, In/Out strip and its own Details. A shared footer. A per-station "⋯" with Pause/Resume, Show in tray and Forget.
    - **Single-station flyout:** unchanged.
    - **"Battery level only" section** (`#adv-only`): advertised percentage, a "battery level only" tag, the reason and one action.
    - **Sign-in window** (`#signin`).
    - **Add station window** (`#add-station`).
    - **Context menu with Show in tray** (`#menu-multi`).
    - **DELTA 2 glyph** (`#glyph-delta2`).

    The flyout section becomes a reusable `StationView` control. The flyout keeps the 360 px width and scrolls inside the work area. A list of tabs was rejected: it hides the second UPS, which is the point of watching both.

12. **Glyphs per model.**
    - `tools/gen_station_glyph.py` is generalised to a shape list per model.
    - It writes `assets/station/<model>.svg` and a generated `Flyout/Glyphs/<Model>Glyph.xaml`, with standard part names: gauge track and value, AC out, DC out, AC in, DC in, optional light.
    - The DELTA 2 shapes come from the owner's `assets/station/delta2-outline.svg` (1086×1448, front view, delivered 2026-10-19).
      - It shows the shell, handle, screen, four USB-A and two USB-C ports in one panel, and the power button. The AC outlets, 12 V and inputs are at the back, so the glyph's live parts are the screen gauge and the USB group (DC out). AC in/out and 12 V appear in the In/Out strip.
      - The portrait aspect (about 0.75) gives a 72×96 glyph in the same 104×96 box as the EB3A, centred.
      - Its 9–13 px strokes are thickened for this size like the EB3A's, and the tiny indicator dots are dropped.
    - Rejected: hand-editing glyph XAML, because it drifts from the SVG.

13. **Logging and privacy.** Never log the email, password or user ID. The log names stations by display name and serial.

14. **Attribution.** `THIRD-PARTY-NOTICES.md` credits rabits/ha-ef-ble (Apache-2.0) for the EcoFlow protocol knowledge. The ported files carry a short header naming the source.

## Risks / Trade-offs

- [Heartbeat layouts change with firmware] → Decoders check minimum lengths and keep unknown fields optional. Captured frames go into tests. A layout mismatch shows values as unavailable rather than wrong.
- [DELTA 2 refuses reconnects for 15–20 s] → Minimum 5 s delay, then retry on "service missing" within the normal backoff. Covered by a monitor test with a fake channel.
- [Two links for hours on one adapter] → Owner hardware task for a multi-hour run with both stations. The log keeps per-station link events.
- [EcoFlow changes the login API or the station rebinds] → Clear error text. The rebind guidance comes from the observed `06`. Advertisement-only mode keeps a level visible.
- [The key table URL or commit disappears] → The pinned hash protects integrity. The script fails with a clear message, and the app keeps working with the EB3A and the advertised DELTA 2 level.
- [Phone app holds the DELTA 2] → Per-station pause, same as for the EB3A. The advertised level is shown while the station still advertises.
- [More UI per station makes the flyout tall] → Sections collapse to one line when not the tray station (decided in the mock-up). The flyout scrolls within the work area.

## Migration Plan

- First start of the new version: back up `settings.json` to `settings.v1.json`, write v2 with the EB3A as the only and tray station, and keep the threshold and autostart.
- Rollback: an older build reads `StationAddress`, which v2 no longer writes. Restoring `settings.v1.json` brings it back, and the README notes this.
- `install.ps1`: fetch the key table if missing, then install as before.
