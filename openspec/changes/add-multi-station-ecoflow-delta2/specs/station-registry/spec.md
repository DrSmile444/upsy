# Spec Delta

## Purpose

Defines the list of power stations upsy watches: how stations are added, named, chosen for the tray icon, forgotten, and carried over from the single-station settings.

## ADDED Requirements

### Requirement: Supported models
The app SHALL recognise a station as supported only when its model has a driver: the Bluetti EB3A (advertising name beginning with `EB3A`) and the EcoFlow DELTA 2 (advertisement manufacturer data `0xB5B5` whose serial number begins with `R331` or `R335`). Other stations SHALL be ignored.

#### Scenario: DELTA 2 advertisement
- **WHEN** a device advertises manufacturer data `0xB5B5` with serial `R331EXAMPLE00001`
- **THEN** it is recognised as an EcoFlow DELTA 2 with that serial

#### Scenario: Unsupported device
- **WHEN** a device advertises a name or serial that matches no driver
- **THEN** it is not offered as a station

### Requirement: Station list
The app SHALL keep a list of stations, each with its model, Bluetooth address, serial number when known, and display name, and SHALL persist the list between runs. Each station in the list SHALL have its own link, state, pause and alerts, independent of the others.

#### Scenario: Two stations
- **WHEN** the list holds an EB3A and a DELTA 2 and both are in range
- **THEN** both are connected and their data is shown at the same time

#### Scenario: One station unreachable
- **WHEN** the DELTA 2 goes out of range while the EB3A stays connected
- **THEN** only the DELTA 2 shows a disconnected state and the EB3A keeps updating

### Requirement: First station
When the station list is empty, the app SHALL add the first supported station it finds and SHALL remember it, so a single-station user needs no setup step.

#### Scenario: First start
- **WHEN** the app starts with an empty list and an EB3A is advertising
- **THEN** the EB3A is added to the list and connected

### Requirement: Add a station
The app SHALL let the user add more stations from a list of supported stations advertising nearby that are not yet in the list, showing each one's model and name or serial number.

#### Scenario: DELTA 2 nearby
- **WHEN** the list holds the EB3A and the user chooses Add station while a DELTA 2 named `R33-0001` is advertising
- **THEN** the DELTA 2 is offered, and choosing it adds it to the list and starts its link

#### Scenario: Nothing new nearby
- **WHEN** the user chooses Add station and no unlisted supported station is advertising
- **THEN** the app says that no new station was found and keeps looking while the list is open

### Requirement: Forget a station
The app SHALL let the user forget a station after one confirmation that names the station and says it can be added again, which ends its link, removes it from the list and stops its alerts. Cancelling SHALL keep the station unchanged.

#### Scenario: Forget the DELTA 2
- **WHEN** the user chooses Forget for the DELTA 2 and confirms
- **THEN** its link is released, it disappears from the flyout, and it is not reconnected after a restart

#### Scenario: Cancel forgetting
- **WHEN** the user chooses Forget and then Cancel
- **THEN** the station stays in the list and keeps its link

### Requirement: Display names
Each station SHALL have a display name that defaults to its model name, with a short serial suffix only when two listed stations share a model.

#### Scenario: Different models
- **WHEN** the list holds an EB3A and a DELTA 2
- **THEN** they are named `EB3A` and `DELTA 2`

#### Scenario: Same model twice
- **WHEN** the list holds two DELTA 2 stations with serials ending `0140` and `0455`
- **THEN** they are named `DELTA 2 ·0140` and `DELTA 2 ·0455`

### Requirement: Tray station
The app SHALL let the user choose which listed station the tray icon shows, SHALL remember the choice, and SHALL fall back to the first listed station when the chosen one is forgotten.

#### Scenario: Choose the DELTA 2
- **WHEN** the user chooses the DELTA 2 for the tray
- **THEN** the tray icon and its tooltip lead with the DELTA 2, also after a restart

#### Scenario: Tray station forgotten
- **WHEN** the tray station is forgotten while the EB3A remains
- **THEN** the tray icon shows the EB3A

### Requirement: Settings migration
On the first start after this change, the app SHALL turn the single remembered station address into a one-entry station list for that station, keeping the low-battery threshold and Start with Windows unchanged.

#### Scenario: Upgrade with a remembered EB3A
- **WHEN** settings hold only the address of an EB3A
- **THEN** the list holds that EB3A, it is the tray station, and it connects without a new scan
