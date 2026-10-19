# low-battery-alerts Specification

## Purpose
Defines the Windows notifications that warn the user when the UPS is running low or its mains power changes.

## Requirements

### Requirement: Low battery notification
While on battery, the app SHALL show a Windows notification once when the battery percentage falls to or below the configured threshold (default 20%).

#### Scenario: Crossing the threshold
- **WHEN** the battery drops from 21% to 20% on battery
- **THEN** one notification shows the percentage and remaining runtime

#### Scenario: Already low when connecting
- **WHEN** the first snapshot of a connection shows on battery at 15% with a 20% threshold
- **THEN** one low-battery notification is shown

#### Scenario: No repeat spam
- **WHEN** the battery stays below the threshold
- **THEN** no further low-battery notifications appear until it has risen above the threshold and fallen again

### Requirement: Critical battery notification
While on battery, the app SHALL show one more notification when the battery falls to or below 10%, with the same re-arm rules as the low-battery notification.

#### Scenario: Crossing 10%
- **WHEN** the battery drops from 11% to 10% on battery
- **THEN** one critical notification shows the percentage, the remaining runtime and advice to shut down or hibernate

### Requirement: Power-loss and restore notifications
The app SHALL notify when mains power is lost and when it is restored, ignoring changes shorter than 5 seconds.

#### Scenario: Mains lost
- **WHEN** the station moves from on mains to on battery and stays there for 5 seconds
- **THEN** a notification states that mains power was lost with the current charge

#### Scenario: Mains restored
- **WHEN** the station returns to on mains and stays there for 5 seconds
- **THEN** a notification states that mains power is back

#### Scenario: Brief glitch
- **WHEN** the source flips for less than 5 seconds
- **THEN** no notification is shown

### Requirement: No alerts without data
The app SHALL NOT raise battery or power alerts based on stale data while disconnected or paused, and SHALL NOT treat the state found when a connection is established as a transition.

#### Scenario: Link lost
- **WHEN** the station becomes unreachable
- **THEN** no low-battery or power-loss notification is raised from the last known values

#### Scenario: App starts while already on battery
- **WHEN** the app connects and the first snapshot shows on battery at 50%
- **THEN** no power-loss notification is raised
