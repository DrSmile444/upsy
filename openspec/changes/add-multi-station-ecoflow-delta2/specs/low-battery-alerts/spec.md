# Spec Delta

## MODIFIED Requirements

### Requirement: Low battery notification
For each listed station that is on battery, the app SHALL show a Windows notification once when its battery percentage falls to or below the configured threshold (default 20%, shared by all stations), naming the station.

#### Scenario: Crossing the threshold
- **WHEN** the EB3A drops from 21% to 20% on battery
- **THEN** one notification titled with `EB3A` shows the percentage and remaining runtime

#### Scenario: Already low when connecting
- **WHEN** the first snapshot of a connection shows on battery at 15% with a 20% threshold
- **THEN** one low-battery notification is shown for that station

#### Scenario: No repeat spam
- **WHEN** the battery stays below the threshold
- **THEN** no further low-battery notifications appear for that station until it has risen above the threshold and fallen again

#### Scenario: Two stations low
- **WHEN** the EB3A and the DELTA 2 both cross 20% on battery
- **THEN** each gets its own notification, titled with its name

### Requirement: Critical battery notification
For each listed station that is on battery, the app SHALL show one more notification when its battery falls to or below 10%, with the same re-arm rules as the low-battery notification, naming the station.

#### Scenario: Crossing 10%
- **WHEN** the DELTA 2 drops from 11% to 10% on battery
- **THEN** one critical notification titled with `DELTA 2` shows the percentage, the remaining runtime and advice to shut down or hibernate

### Requirement: Power-loss and restore notifications
The app SHALL notify, per station and naming it, when mains power is lost and when it is restored, ignoring changes shorter than 5 seconds.

#### Scenario: Mains lost
- **WHEN** the DELTA 2 moves from on mains to on battery and stays there for 5 seconds
- **THEN** a notification titled with `DELTA 2` states that mains power was lost with the current charge

#### Scenario: Mains restored
- **WHEN** a station returns to on mains and stays there for 5 seconds
- **THEN** a notification titled with its name states that mains power is back

#### Scenario: Brief glitch
- **WHEN** the source flips for less than 5 seconds
- **THEN** no notification is shown

### Requirement: No alerts without data
The app SHALL NOT raise battery or power alerts for a station from stale data while it is disconnected or paused, nor from a battery level read only from its advertisement, and SHALL NOT treat the state found when a connection is established as a transition.

#### Scenario: Link lost
- **WHEN** the station becomes unreachable
- **THEN** no low-battery or power-loss notification is raised from the last known values

#### Scenario: App starts while already on battery
- **WHEN** the app connects and the first snapshot shows on battery at 50%
- **THEN** no power-loss notification is raised

#### Scenario: Battery level only
- **WHEN** the DELTA 2 is shown from its advertisement at 15%
- **THEN** no low-battery notification is raised for it
