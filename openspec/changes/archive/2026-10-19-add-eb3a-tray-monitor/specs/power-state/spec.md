# Spec Delta

## Purpose

Defines the derived values the app shows that the station does not report itself: power source, charging, load presence, and remaining runtime.

## ADDED Requirements

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
While on battery, not charging and with a load present, the app SHALL estimate remaining runtime from the battery percentage, the station's 268 Wh nominal capacity, a usable-energy factor, and a smoothed average of discharge power (total output minus DC input), and SHALL present it as an approximation.

#### Scenario: Fluctuating load
- **WHEN** output power varies between 118 and 156 W at 56% charge
- **THEN** the estimate changes smoothly rather than jumping with each sample

#### Scenario: Not enough data yet
- **WHEN** fewer than about 10 seconds of power samples exist since connecting
- **THEN** no runtime is shown

#### Scenario: Very small load
- **WHEN** the estimate exceeds 24 hours
- **THEN** the runtime is shown as more than 24 hours

#### Scenario: No load, charging or on mains
- **WHEN** no load is present, the station is charging, or it is on mains
- **THEN** no remaining runtime is shown and the state is shown instead
