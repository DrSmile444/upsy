# Proposal

## Why

The owner now runs two portable power stations as UPS units: the Bluetti EB3A and an EcoFlow DELTA 2. upsy can watch only one Bluetti station today, so the DELTA 2 is visible only on its own screen and in the EcoFlow phone app. A spike (2026-10-19, `local/efspike`) read the owner's DELTA 2 from Windows end to end: battery, AC/DC power, mains voltage, output switches and runtime, with no third-party library, while upsy kept its EB3A link. Both stations can now be shown together, and the code can be opened up for more models.

## What Changes

- **Several stations at once.** upsy keeps a list of stations, runs one link per station, and shows all of them in the flyout. The tray icon shows one station, chosen by the user. Each station has its own pause, alerts and details. Stations are added from a list of supported stations found nearby and can be forgotten.
- **Vendor drivers.** Bluetti (Modbus polling) and EcoFlow (encrypted session, pushed heartbeats) sit behind one driver interface that produces the same telemetry, identity and settings model. Adding a model means adding a driver, a glyph and a capacity, not touching the monitor, alerts or UI.
- **EcoFlow DELTA 2, read-only, at EB3A parity:**
  - discovery from its BLE advertisement (manufacturer data `0xB5B5`, serial prefix `R331`/`R335`);
  - the ECDH session and account authentication the station requires;
  - decoding of its heartbeats: battery, AC/DC input and output power, mains voltage, output switches, the station's own remaining time, identity and settings.
- **EcoFlow account sign-in.** The station only talks to the EcoFlow account it is bound to. upsy asks for the email and password once, gets the account's user ID from EcoFlow, keeps only the user ID (encrypted for the Windows user), and never stores the password. If the station rejects the key, upsy explains how to rebind the station in the EcoFlow app.
- **Battery level without a session.** When there is no account or key table, upsy still shows the DELTA 2 battery level from its advertisement and says what is missing. This also covers the time when the phone app holds the link.
- **EcoFlow key table stays out of git.** The handshake needs a constant table from EcoFlow's app. upsy loads it from a local file, and a script fetches it from a pinned upstream commit and checks its hash.
- **Design first.** The canvas `design/upsy.design.html` gains the multi-station flyout, the tray station choice, the EcoFlow sign-in, the "battery level only" state and the DELTA 2 glyph. The owner draws the glyph outline; the mock-up is reviewed and accepted before the UI is built.
- Settings migrate: the single remembered EB3A becomes the first entry of the station list, with nothing for the user to redo.

Out of scope: controlling either station (outputs, charge limits, X-Boost), EcoFlow cloud/MQTT data, other EcoFlow models, one tray icon per station.

Later: other EcoFlow models on the same protocol (DELTA 2 Max, River 2/3, DELTA 3); more Bluetti models; a combined "total backup" view; hibernate or shut down the PC at critical charge (carried over).

## Capabilities

### New Capabilities
- `station-registry`: the list of stations, adding stations found nearby, forgetting them, naming them, choosing the tray station, and migrating the single-station settings.
- `ecoflow-account`: signing in to EcoFlow to get the user ID, storing it safely, signing out, and explaining rejected keys.

### Modified Capabilities
- `station-link`: discovery, telemetry updates, identity and settings become per station and per vendor. Adds the EcoFlow encrypted session, pushed heartbeats, battery level from the advertisement, and a reconnect delay for stations that refuse a quick reconnect. Pause becomes per station. Read-only now allows the session and authentication messages a station requires, but still no control commands.
- `power-state`: nominal capacity comes from the station model, and a remaining time reported by the station is used instead of the estimate.
- `tray-ui`: the tray icon follows the chosen station, the tooltip and flyout cover every station, the menu gains station choice and Add station, and the flyout gains the DELTA 2 glyph, the sign-in prompt and the "battery level only" state.
- `low-battery-alerts`: alerts run per station and name the station.

## Impact

- **Code.** `Upsy.Core`:
  - the session interface becomes a driver interface;
  - new EcoFlow protocol code: frames, CRC, crypto, heartbeat decoding;
  - per-station models and capacities;
  - a registry and per-station monitors.

  `Upsy`:
  - a WinRT EcoFlow transport;
  - multi-station tray and flyout;
  - a sign-in window;
  - DPAPI storage and settings migration.

  `tools/install.ps1` gains the key table step.
- **Dependencies.** None new: .NET's `ECDiffieHellman` (CNG, secp160r1), `Aes`, `MD5` and `HttpClient` cover the protocol and the login.
- **External.** The EcoFlow login endpoint (`https://api.ecoflow.com/auth/login`) is called once, only when the user signs in.
- **Licensing.**
  - The protocol port follows rabits/ha-ef-ble (Apache-2.0): add a THIRD-PARTY-NOTICES file.
  - The key table is EcoFlow's and is never committed.
- **Assets.** `assets/station/delta2-outline.svg` from the owner; a generated `delta2.svg` and its WPF glyph.
- **Risks to validate on hardware:**
  - two links held for hours;
  - the DELTA 2 refusing reconnects for 15–20 s after a drop;
  - heartbeat layouts changing with firmware;
  - DC output fields untested under a real DC load;
  - phone-app coexistence for two stations.
