# Spec Delta

## Purpose

Defines how the app presents station status in the Windows notification area, in the native style of Windows 11.

## ADDED Requirements

### Requirement: Tray icon
The app SHALL show a notification-area icon with the battery percentage that is legible at the current DPI, follows the Windows light or dark theme, and indicates the state (on battery, charging, low, critical, disconnected, paused) by shape and colour, never by colour alone.

#### Scenario: Theme change
- **WHEN** the user switches Windows between light and dark theme
- **THEN** the icon is redrawn in a colour that contrasts with the taskbar

#### Scenario: Low charge
- **WHEN** the battery is at or below the low threshold on battery
- **THEN** the icon shows a warning colour

#### Scenario: Disconnected
- **WHEN** the station is unreachable
- **THEN** the icon shows a distinct disconnected glyph instead of a stale percentage

#### Scenario: Paused
- **WHEN** the user has paused the connection
- **THEN** the icon shows a paused glyph that differs from the disconnected glyph

#### Scenario: Keyboard
- **WHEN** the icon has keyboard focus in the notification area and the user presses Enter
- **THEN** the flyout opens with focus inside it

### Requirement: Tooltip summary
On hover, the app SHALL show the station name, battery percentage, current output power, and remaining runtime or charging state, within the 127-character tooltip limit.

#### Scenario: On battery
- **WHEN** the user hovers while on battery at 58% and 128 W
- **THEN** the tooltip reads like `EB3A · 58% · 128 W out · about 1 h left`

#### Scenario: Charging
- **WHEN** the user hovers while charging
- **THEN** the tooltip shows percentage, power and that it is charging, not a runtime

### Requirement: Details flyout
Left-clicking the icon SHALL open a flyout near the tray, in the current light or dark theme, with percentage, remaining runtime or charging state, and current power, plus an expandable section listing everything the station reports: AC and DC input/output power, AC and DC input voltage, AC and DC output on/off, net battery power, load present, model, serial number, firmware versions, and the station settings when available.

#### Scenario: Expand details
- **WHEN** the user expands the details section
- **THEN** every reported value is listed with its label and unit

#### Scenario: Disconnected flyout
- **WHEN** the flyout is opened while no data is available
- **THEN** it shows the disconnected reason instead of stale values

#### Scenario: Dismiss
- **WHEN** the user clicks elsewhere or presses Escape
- **THEN** the flyout closes

### Requirement: Station glyph
The flyout SHALL show a small (about 96 px wide) vector glyph of the recognised station model, selected from the model reported by the station, whose parts reflect live data: the battery fill level and state colour, the AC and DC output groups highlighted when switched on with their power, and the active input highlighted with its power. It SHALL stay sharp at any display scaling and follow the theme. An unrecognised model SHALL show a generic battery glyph with the reported model name.

#### Scenario: EB3A recognised
- **WHEN** the station reports model `EB3A`
- **THEN** the EB3A glyph is shown with its screen filled to the current percentage

#### Scenario: Outputs and inputs
- **WHEN** AC output is on at 128 W, DC output is off, and mains supplies 390 W
- **THEN** the AC outlets are highlighted with 128 W, the DC outputs appear inactive, and the AC input is highlighted with 390 W

#### Scenario: Unknown model
- **WHEN** the station reports a model without a dedicated glyph
- **THEN** a generic battery glyph and the reported model name are shown

### Requirement: Context menu and settings
Right-clicking the icon SHALL offer Open upsy (default), Pause for 30 minutes / Resume, a Start with Windows toggle, the low-battery threshold (10-30%), and Exit, and the app SHALL persist these settings between runs.

#### Scenario: Start with Windows
- **WHEN** the user enables Start with Windows
- **THEN** the app launches at the next sign-in

#### Scenario: Exit
- **WHEN** the user chooses Exit
- **THEN** the icon is removed and the process ends

### Requirement: Runtime wording
Runtime SHALL be shown as "about" a value rounded to 5 minutes below one hour and to 10 minutes from one hour, and more than 24 hours SHALL read "more than 24 h".

#### Scenario: Rounding
- **WHEN** the estimate is 1 h 2 min
- **THEN** the UI reads "about 1 h"

#### Scenario: Short runtime
- **WHEN** the estimate is 21 minutes
- **THEN** the UI reads "about 20 min"

### Requirement: Single instance
The app SHALL run at most one instance per user session.

#### Scenario: Second launch
- **WHEN** the app is started while already running
- **THEN** the new process exits without adding a second icon
