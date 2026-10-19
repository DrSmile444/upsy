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
While on battery, not charging and with a load present, the app SHALL show the remaining runtime and SHALL say where it comes from. When the station reports its own time to empty (DELTA 2), the app SHALL use that value as reported. Otherwise it SHALL estimate runtime from the battery percentage, the model's nominal capacity (EB3A 268 Wh, DELTA 2 1024 Wh), a usable-energy factor, and a smoothed average of discharge power (total output minus DC input), and SHALL present it as an estimate.

#### Scenario: Fluctuating load
- **WHEN** EB3A output power varies between 118 and 156 W at 56% charge
- **THEN** the estimate changes smoothly rather than jumping with each sample

#### Scenario: Station-reported runtime
- **WHEN** the DELTA 2 is on battery at 82% with 103 W on the AC output and reports 431 minutes to empty
- **THEN** the app shows 7 h 11 min left, marked as the station's value

#### Scenario: Estimated runtime
- **WHEN** the EB3A is on battery at 58% with a steady 128 W load
- **THEN** the app shows about 1 h left, marked as an estimate

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
While charging, the app SHALL show the time to full and SHALL say where it comes from. When the station reports its own time to full (DELTA 2), the app SHALL use that value as reported. Otherwise it SHALL estimate it from the model's nominal capacity, the battery percentage, the model's charge efficiency, and the net charge power (total input minus total output) smoothed over a few minutes, SHALL present it as an estimate, and SHALL show it only after about a minute of charging samples. It SHALL show no time to full when the station is not charging, including when net charge power is 10 W or less.

#### Scenario: DELTA 2 charging
- **WHEN** the DELTA 2 charges at 80% from 328 W of mains and reports 73 minutes to full
- **THEN** the app shows charging with 1 h 13 min to full, marked as the station's value

#### Scenario: EB3A charging
- **WHEN** the EB3A has charged steadily for a minute at 54% with 390 W in and 126 W out
- **THEN** the app shows charging at about +264 W with a time to full of 268 Wh × 46% ÷ (264 W × the EB3A charge efficiency), worded like "Full in about 35 min, estimated"

#### Scenario: Load cycling while charging
- **WHEN** the EB3A charges while its output switches between 40 W and 130 W every few minutes
- **THEN** the time to full changes smoothly rather than jumping with each switch

#### Scenario: Charging paused
- **WHEN** the EB3A stays on mains below 100% but input equals output (for example during a thermal pause)
- **THEN** the app shows no time to full

#### Scenario: Just plugged in
- **WHEN** mains has been back for less than about a minute
- **THEN** the app shows the charging power without a time to full

### Requirement: Energy log for calibration
While a station charges, or discharges into a load on battery, the app SHALL write one line a minute to its log with the station name, the direction, the battery percentage, the average input and output power over that minute, and the energy accumulated since that charge or discharge started, integrated over every sample. A charge or discharge interrupted for more than a minute, or a change of direction, SHALL start a new accumulation.

#### Scenario: Logged discharge and charge
- **WHEN** the EB3A runs on battery from 100% to 40% with its load cycling, then charges back to 100%
- **THEN** the log holds one discharge line and one charge line per minute, from which the energy taken out and put in can be compared with the percentage lost and gained

#### Scenario: Idle
- **WHEN** the station is full on mains, paused in charging, or on battery without a load
- **THEN** no energy log lines are written
