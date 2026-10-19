# Spec Delta

## Purpose

Defines how the app finds a Bluetti EB3A over Bluetooth LE, reads its telemetry without pairing, and keeps the link alive across drops.

## ADDED Requirements

### Requirement: Discover the station
The app SHALL locate an EB3A by its Bluetooth LE advertising name beginning with `EB3A` and SHALL remember its address so later starts reconnect directly.

#### Scenario: First start with the station in range
- **WHEN** the app starts with no remembered station and an EB3A is advertising
- **THEN** the app connects to it and stores its address

#### Scenario: Station not in range
- **WHEN** no EB3A is advertising
- **THEN** the app keeps scanning and reports a disconnected state

### Requirement: Read telemetry without pairing
The app SHALL read station data over BLE without requiring pairing, a PIN, or any Windows Bluetooth settings step.

#### Scenario: Unpaired station
- **WHEN** the station is not paired with Windows
- **THEN** the app still obtains battery, power, voltage and identity data

### Requirement: Periodic polling
While connected, the app SHALL refresh telemetry about every 2 seconds and SHALL discard any response whose checksum is invalid or that does not arrive within 4 seconds.

#### Scenario: Valid response
- **WHEN** the station answers a read request with a valid checksum
- **THEN** the telemetry snapshot is updated with the battery percentage, DC/AC input and output power, AC and DC input voltage, and AC/DC output-on flags

#### Scenario: Response split across notifications
- **WHEN** a response arrives in several BLE notifications
- **THEN** the parts are joined and validated as one frame

#### Scenario: Missing or corrupt response
- **WHEN** a response is late or fails its checksum
- **THEN** the previous snapshot is kept and the request is retried on the next cycle

### Requirement: Read-only operation
The app SHALL NOT write any control or configuration register on the station.

#### Scenario: Normal operation
- **WHEN** the app runs for any length of time
- **THEN** it sends only register read requests

### Requirement: Identity data
The app SHALL decode the station's model, serial number and ARM/DSP firmware versions once per connection.

#### Scenario: Connection established
- **WHEN** a connection is established
- **THEN** model, serial and firmware versions are available to the UI

### Requirement: Station settings readout
The app SHALL read the station's ECO mode, ECO shutdown time, charging mode, power lifting and LED mode once per connection and then about once a minute, and SHALL omit them without error if the station does not answer those reads.

#### Scenario: Settings available
- **WHEN** the station answers the settings read
- **THEN** ECO, charging mode, power lifting and LED mode are available to the UI

#### Scenario: Settings not supported
- **WHEN** the settings read times out or returns an error
- **THEN** telemetry polling continues and those values are shown as unavailable

### Requirement: Automatic reconnection
When the link drops or polling keeps failing, the app SHALL reconnect automatically with increasing delay of at most 30 seconds, without user action.

#### Scenario: Station comes back
- **WHEN** the station was unreachable and becomes reachable again
- **THEN** the app resumes polling and clears the disconnected state within 30 seconds

#### Scenario: Transient connect failure
- **WHEN** an initial connection attempt fails
- **THEN** the app retries instead of giving up

#### Scenario: PC resumes from sleep
- **WHEN** Windows resumes from sleep or hibernation
- **THEN** the app discards the old connection and reconnects without user action

### Requirement: Disconnected reason
The app SHALL report why it has no data: Bluetooth is off, searching for the station, connecting, or paused by the user.

#### Scenario: Bluetooth radio off
- **WHEN** the PC's Bluetooth radio is turned off
- **THEN** the state reads that Bluetooth is off, and polling resumes after the radio is turned back on

### Requirement: Pause connection
The app SHALL let the user pause the connection for 30 minutes and resume it earlier; while paused it SHALL release the BLE link and SHALL NOT scan or reconnect, and it SHALL resume by itself when the 30 minutes end.

#### Scenario: Hand over to the phone app
- **WHEN** the user chooses Pause for 30 minutes
- **THEN** the BLE link is released so another client can connect, and the state reads paused

#### Scenario: Resume
- **WHEN** the user chooses Resume
- **THEN** the app reconnects and polling resumes

#### Scenario: Forgotten pause
- **WHEN** 30 minutes pass after the user paused
- **THEN** the app resumes the connection by itself
