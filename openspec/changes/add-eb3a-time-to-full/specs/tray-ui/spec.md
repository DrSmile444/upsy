# Spec Delta

## MODIFIED Requirements

### Requirement: Tooltip summary
On hover, the app SHALL show the tray station's name, battery percentage, current output power, and remaining runtime or charging state, followed by a short entry (name and percentage) for each other station, within the 127-character tooltip limit, dropping the other stations' entries first when space runs out. A runtime upsy estimated SHALL be marked as estimated.

#### Scenario: On battery
- **WHEN** the user hovers while the EB3A is on battery at 58% and 128 W and is the only station
- **THEN** the tooltip reads like `EB3A · 58% · 128 W out · about 1 h left (estimated)`

#### Scenario: Station-reported runtime
- **WHEN** the user hovers while the DELTA 2 is the tray station on battery and reports 431 minutes to empty
- **THEN** the tooltip reads like `DELTA 2 · 82% · 103 W out · 7 h 11 min left`

#### Scenario: Two stations
- **WHEN** the tray station is the EB3A at 100% on mains and the DELTA 2 is at 82%
- **THEN** the tooltip reads like `EB3A · 100% · on mains` followed by `DELTA 2 · 82%` on the next line

#### Scenario: Charging
- **WHEN** the user hovers while the tray station is charging
- **THEN** the tooltip shows percentage, power and that it is charging, not a runtime

### Requirement: Runtime wording
A time upsy estimated (runtime or time to full) SHALL be shown as "about" a value rounded to 5 minutes below one hour and to 10 minutes from one hour, and SHALL be marked as estimated where there is room for it (the line under the status, the Details row, the tooltip and notifications). A time the station reports SHALL be shown as reported, to the minute, without "about", and SHALL be marked as the station's value in the Details row. More than 24 hours SHALL read "more than 24 h" in both cases.

#### Scenario: Rounding
- **WHEN** the estimate is 1 h 2 min
- **THEN** the UI reads "about 1 h"

#### Scenario: Short runtime
- **WHEN** the estimate is 21 minutes
- **THEN** the UI reads "about 20 min"

#### Scenario: Reported to the minute
- **WHEN** the DELTA 2 reports 73 minutes to full
- **THEN** the UI reads "1 h 13 min", and the Details row reads "1 h 13 min (station)"

#### Scenario: Estimate is marked
- **WHEN** upsy estimates 35 minutes to full for the EB3A
- **THEN** the line under the status reads "Full in about 35 min, estimated" and the Details row reads "About 35 min (estimated)"
