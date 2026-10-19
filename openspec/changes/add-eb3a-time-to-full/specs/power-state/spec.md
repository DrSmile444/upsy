# Spec Delta

## MODIFIED Requirements

### Requirement: Time to full
While charging, the app SHALL show the time to full as an approximation. When the station reports its own time to full (DELTA 2), the app SHALL use that value. Otherwise it SHALL estimate it from the model's nominal capacity, the battery percentage, the model's charge efficiency, and the net charge power (total input minus total output) smoothed over a few minutes, and SHALL show it only after about a minute of charging samples. It SHALL show no time to full when the station is not charging, including when net charge power is 10 W or less.

#### Scenario: DELTA 2 charging
- **WHEN** the DELTA 2 charges at 80% from 328 W of mains and reports 73 minutes to full
- **THEN** the app shows charging with about 1 h 10 min to full

#### Scenario: EB3A charging
- **WHEN** the EB3A has charged steadily for a minute at 54% with 390 W in and 126 W out
- **THEN** the app shows charging at about +264 W with a time to full of 268 Wh × 46% ÷ (264 W × the EB3A charge efficiency), worded like "Full in about 35 min"

#### Scenario: Load cycling while charging
- **WHEN** the EB3A charges while its output switches between 40 W and 130 W every few minutes
- **THEN** the time to full changes smoothly rather than jumping with each switch

#### Scenario: Charging paused
- **WHEN** the EB3A stays on mains below 100% but input equals output (for example during a thermal pause)
- **THEN** the app shows no time to full

#### Scenario: Just plugged in
- **WHEN** mains has been back for less than about a minute
- **THEN** the app shows the charging power without a time to full

## ADDED Requirements

### Requirement: Charge log for calibration
While a station charges, the app SHALL write one line a minute to its log with the station name, the battery percentage, the average input and output power over that minute, and the charge energy accumulated since charging started, integrated over every sample. A charge interrupted for more than a minute SHALL start a new accumulation.

#### Scenario: Logged charge
- **WHEN** the EB3A charges from 40% to 100% while its load cycles
- **THEN** the log holds one line per minute from which the energy that went in and the percentage gained can be compared

#### Scenario: Not charging
- **WHEN** the station is on battery, full, or paused in charging
- **THEN** no charge log lines are written
