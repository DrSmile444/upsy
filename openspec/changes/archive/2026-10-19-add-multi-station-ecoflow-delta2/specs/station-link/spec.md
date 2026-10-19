# Spec Delta

## MODIFIED Requirements

### Requirement: Discover the station
For each listed station, the app SHALL find it by its Bluetooth address, and SHALL fall back to its model's advertisement (EB3A: name beginning with `EB3A`; DELTA 2: manufacturer data `0xB5B5` with the station's serial) when the address does not answer, updating the stored address when it changed.

#### Scenario: First start with the station in range
- **WHEN** the app starts with no listed station and an EB3A is advertising
- **THEN** the app connects to it and stores its address

#### Scenario: Station not in range
- **WHEN** a listed station is not advertising
- **THEN** the app keeps looking for it and reports that station as disconnected, without affecting other stations

### Requirement: Read telemetry without pairing
The app SHALL read station data over BLE without requiring pairing, a PIN, or any Windows Bluetooth settings step, for every supported model.

#### Scenario: Unpaired station
- **WHEN** the station is not paired with Windows
- **THEN** the app still obtains battery, power, voltage and identity data

#### Scenario: Unpaired DELTA 2
- **WHEN** the DELTA 2 is not paired with Windows and an EcoFlow user ID is stored
- **THEN** the app obtains its battery, power, voltage and identity data

### Requirement: Periodic polling
While connected, the app SHALL keep each station's telemetry current: it SHALL poll stations that answer requests (EB3A) about every 2 seconds and discard any response whose checksum is invalid or that does not arrive within 4 seconds, and it SHALL accept updates from stations that push data (DELTA 2), discard any frame whose checksum or decryption fails, and treat the link as failing when no complete update arrives for 10 seconds.

#### Scenario: Valid response
- **WHEN** the EB3A answers a read request with a valid checksum
- **THEN** the telemetry snapshot is updated with the battery percentage, DC/AC input and output power, AC and DC input voltage, and AC/DC output-on flags

#### Scenario: Response split across notifications
- **WHEN** a response or pushed frame arrives in several BLE notifications
- **THEN** the parts are joined and validated as one frame

#### Scenario: Missing or corrupt response
- **WHEN** an EB3A response is late or fails its checksum
- **THEN** the previous snapshot is kept and the request is retried on the next cycle

#### Scenario: Pushed update
- **WHEN** the DELTA 2 pushes its heartbeats while discharging at 82% with 103 W on the AC output
- **THEN** the snapshot shows 82%, 103 W AC output, AC output on, and 0 W input, updated about every second

#### Scenario: Pushed updates stop
- **WHEN** no valid DELTA 2 update arrives for 10 seconds
- **THEN** the link is treated as failing and reconnection starts

### Requirement: Read-only operation
The app SHALL NOT write any control or configuration value on a station. It SHALL send only register read requests to Bluetti stations, and only the key exchange, session key request, authentication status and authentication messages to EcoFlow stations.

#### Scenario: Normal operation
- **WHEN** the app runs for any length of time
- **THEN** it sends only the reads and session messages above, never a command that changes an output, limit or setting

### Requirement: Identity data
The app SHALL obtain each station's model, serial number and firmware versions once per connection: for the EB3A from its identity registers (ARM and DSP firmware), and for the DELTA 2 from its advertisement and heartbeats.

#### Scenario: Connection established
- **WHEN** a connection is established
- **THEN** model, serial and firmware versions are available to the UI

#### Scenario: DELTA 2 identity
- **WHEN** the DELTA 2 with serial `R331EXAMPLE00001` connects
- **THEN** the model reads `DELTA 2`, the serial reads `R331EXAMPLE00001`, and its firmware version is shown

### Requirement: Station settings readout
The app SHALL show the settings each model reports, read-only: for the EB3A, ECO mode, ECO shutdown time, charging mode, power lifting and LED mode, read once per connection and then about once a minute; for the DELTA 2, the charge limit, discharge limit, X-Boost and AC charging power, taken from its heartbeats. It SHALL omit settings without error when the station does not report them.

#### Scenario: Settings available
- **WHEN** the EB3A answers the settings read
- **THEN** ECO, charging mode, power lifting and LED mode are available to the UI

#### Scenario: Settings not supported
- **WHEN** the settings read times out or returns an error
- **THEN** telemetry continues and those values are shown as unavailable

#### Scenario: DELTA 2 settings
- **WHEN** the DELTA 2 heartbeats report a 100% charge limit
- **THEN** the charge limit reads 100% in the station's details

### Requirement: Automatic reconnection
When a station's link drops or keeps failing, the app SHALL reconnect it automatically with increasing delay of at most 30 seconds, without user action. After a DELTA 2 link ends, the first reconnect attempt SHALL wait at least 5 seconds and further attempts SHALL continue while the station refuses service discovery.

#### Scenario: Station comes back
- **WHEN** the station was unreachable and becomes reachable again
- **THEN** the app resumes updates and clears the disconnected state within 30 seconds

#### Scenario: Transient connect failure
- **WHEN** an initial connection attempt fails
- **THEN** the app retries instead of giving up

#### Scenario: PC resumes from sleep
- **WHEN** Windows resumes from sleep or hibernation
- **THEN** the app discards the old connections and reconnects every listed station without user action

#### Scenario: DELTA 2 busy after a drop
- **WHEN** the DELTA 2 returns no services for 15–20 seconds after its link ended
- **THEN** the app keeps retrying and connects once the station answers

### Requirement: Disconnected reason
The app SHALL report for each station why it has no live data: Bluetooth is off, searching for the station, connecting, paused by the user, EcoFlow sign-in needed, account key rejected by the station, or EcoFlow key table missing.

#### Scenario: Bluetooth radio off
- **WHEN** the PC's Bluetooth radio is turned off
- **THEN** every station reads that Bluetooth is off, and updates resume after the radio is turned back on

#### Scenario: Sign-in needed
- **WHEN** a DELTA 2 is listed and no EcoFlow user ID is stored
- **THEN** that station reads that EcoFlow sign-in is needed

### Requirement: Pause connection
The app SHALL let the user pause one station's connection for 30 minutes and resume it earlier; while paused it SHALL release that station's BLE link and SHALL NOT scan for or reconnect it, SHALL keep other stations connected, and SHALL resume by itself when the 30 minutes end.

#### Scenario: Hand over to the phone app
- **WHEN** the user pauses the DELTA 2
- **THEN** its BLE link is released so the EcoFlow app can connect, it reads paused, and the EB3A stays connected

#### Scenario: Resume
- **WHEN** the user chooses Resume for a paused station
- **THEN** the app reconnects it and updates resume

#### Scenario: Forgotten pause
- **WHEN** 30 minutes pass after the user paused a station
- **THEN** the app resumes that station by itself

## ADDED Requirements

### Requirement: EcoFlow encrypted session
For EcoFlow stations that advertise encryption type 7, the app SHALL establish the session the station requires (ECDH key exchange, session key from the station's seed and the key table, authentication status) and authenticate with the stored user ID and the station's serial number, before reading any data.

#### Scenario: Authenticated
- **WHEN** the DELTA 2 accepts the authentication
- **THEN** it starts pushing heartbeats and the station shows live data

#### Scenario: Key rejected
- **WHEN** the DELTA 2 answers authentication with the wrong-key code
- **THEN** the app stops authenticating that station and reports that the account key was rejected

### Requirement: EcoFlow key table
The app SHALL read the EcoFlow key table from a local file outside the source repository, SHALL verify its size and checksum before use, and SHALL report a missing or invalid table as a reason, without affecting other stations.

#### Scenario: Table missing
- **WHEN** the key table file is absent
- **THEN** the DELTA 2 reads that the EcoFlow key table is missing, shows its advertised battery level, and the EB3A is unaffected

### Requirement: DELTA 2 telemetry
The app SHALL decode the DELTA 2 heartbeats into the same telemetry as other stations: battery percentage, AC input power and voltage, DC (solar/car) input power, AC output power, DC output power as the sum of its USB, USB-C and 12 V ports, AC/USB/12 V output-on flags, and the station's remaining time to empty or to full.

#### Scenario: On mains and charging
- **WHEN** the DELTA 2 reports 80%, AC input 328 W at 233.3 V and 101 W on the AC output
- **THEN** the snapshot shows 80%, 328 W AC input, 233.3 V, 101 W AC output, and a time to full

#### Scenario: Output switches
- **WHEN** the USB and 12 V outputs are switched on with nothing plugged in
- **THEN** DC output reads on and DC output power reads 0 W

### Requirement: Battery level from the advertisement
When an EcoFlow station has no session (no user ID, key table missing, key rejected, or another client holds the link), the app SHALL show the battery percentage carried in its advertisement, marked as battery level only, and SHALL NOT derive power source, runtime or alerts from it.

#### Scenario: Not signed in
- **WHEN** the DELTA 2 advertises 84% and no user ID is stored
- **THEN** the station shows 84% marked as battery level only, with no runtime and no power-loss alerts

#### Scenario: Level changes
- **WHEN** the advertised level changes from 84% to 87%
- **THEN** the shown level updates to 87% within a few seconds
