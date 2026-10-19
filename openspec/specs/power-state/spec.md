# power-state Specification

## Purpose
Defines the derived values the app shows that the station does not report itself: power source, charging, load presence, and remaining runtime.

## Requirements

### Requirement: Power source state
The app SHALL classify the station as on mains when the AC input voltage is above 100 V, and as on battery otherwise. AC input power SHALL NOT be used for this decision, because it can be 0 W while plugged in.

#### Scenario: Plugged in
- **WHEN** AC input voltage is 228 V
- **THEN** the state is on mains

#### Scenario: Plugged in, full and idle
- **WHEN** AC input voltage is 228 V and AC input power is 0 W
- **THEN** the state is on mains

#### Scenario: Unplugged
- **WHEN** AC input is 0 W and the reported input voltage is a few volts of noise
- **THEN** the state is on battery

### Requirement: Charging indication
The app SHALL compute net battery power as total input (AC + DC) minus total output (AC + DC), SHALL indicate charging when net power exceeds 10 W and the battery is below 100%, and SHALL expose the net charging power.

#### Scenario: Charging while powering a load
- **WHEN** AC input is 390 W and AC output is 126 W at 54%
- **THEN** the app reports charging at about 264 W

#### Scenario: On mains but full
- **WHEN** on mains at 100% and input roughly equals output
- **THEN** the app reports on mains without charging

#### Scenario: Solar input on battery
- **WHEN** on battery with DC input 80 W and AC output 30 W
- **THEN** the app reports charging at about 50 W

### Requirement: Load presence
The app SHALL report a load as present when AC output plus DC output exceeds 20 W.

#### Scenario: PC connected
- **WHEN** AC output is on and draws 130 W
- **THEN** load is present

#### Scenario: Idle inverter
- **WHEN** AC output is on and draws 5 W and DC output is 0 W
- **THEN** load is not present

### Requirement: Remaining runtime on battery
While on battery, not charging and with a load present, the app SHALL show remaining runtime as an approximation. When the station reports its own time to empty (DELTA 2), the app SHALL use that value. Otherwise it SHALL estimate runtime from the battery percentage, the model's nominal capacity (EB3A 268 Wh, DELTA 2 1024 Wh), a usable-energy factor, and a smoothed average of discharge power (total output minus DC input).

#### Scenario: Fluctuating load
- **WHEN** EB3A output power varies between 118 and 156 W at 56% charge
- **THEN** the estimate changes smoothly rather than jumping with each sample

#### Scenario: Station-reported runtime
- **WHEN** the DELTA 2 is on battery at 82% with 103 W on the AC output and reports 431 minutes to empty
- **THEN** the app shows about 7 h 10 min left

#### Scenario: Not enough data yet
- **WHEN** fewer than about 10 seconds of power samples exist since connecting and the station reports no time to empty
- **THEN** no runtime is shown

#### Scenario: Very small load
- **WHEN** the runtime exceeds 24 hours
- **THEN** the runtime is shown as more than 24 hours

#### Scenario: No load, charging or on mains
- **WHEN** no load is present, the station is charging, or it is on mains
- **THEN** no remaining runtime is shown and the state is shown instead

### Requirement: Time to full
While charging, when the station reports its own time to full (DELTA 2), the app SHALL show it as an approximation; stations that do not report it SHALL show the charging power only.

#### Scenario: DELTA 2 charging
- **WHEN** the DELTA 2 charges at 80% from 328 W of mains and reports 73 minutes to full
- **THEN** the app shows charging with about 1 h 10 min to full

#### Scenario: EB3A charging
- **WHEN** the EB3A charges at 54% with 264 W net
- **THEN** the app shows charging at about 264 W and no time to full
