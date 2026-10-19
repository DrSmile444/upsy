# Spec Delta

## MODIFIED Requirements

### Requirement: Tray icon
The app SHALL show one notification-area icon for the tray station chosen by the user, with that station's battery percentage, legible at the current DPI, following the Windows light or dark theme, and indicating the state (on battery, charging, low, critical, disconnected, paused, battery level only) by shape and colour, never by colour alone.

#### Scenario: Theme change
- **WHEN** the user switches Windows between light and dark theme
- **THEN** the icon is redrawn in a colour that contrasts with the taskbar

#### Scenario: Low charge
- **WHEN** the tray station is at or below the low threshold on battery
- **THEN** the icon shows a warning colour

#### Scenario: Disconnected
- **WHEN** the tray station is unreachable
- **THEN** the icon shows a distinct disconnected glyph instead of a stale percentage

#### Scenario: Paused
- **WHEN** the user has paused the tray station
- **THEN** the icon shows a paused glyph that differs from the disconnected glyph

#### Scenario: Another station changes
- **WHEN** the tray station is the EB3A and the DELTA 2 loses mains
- **THEN** the icon keeps showing the EB3A, and the DELTA 2 change reaches the user through its notification and the flyout

#### Scenario: Keyboard
- **WHEN** the icon has keyboard focus in the notification area and the user presses Enter
- **THEN** the flyout opens with focus inside it

### Requirement: Tooltip summary
On hover, the app SHALL show the tray station's name, battery percentage, current output power, and remaining runtime or charging state, followed by a short entry (name and percentage) for each other station, within the 127-character tooltip limit, dropping the other stations' entries first when space runs out.

#### Scenario: On battery
- **WHEN** the user hovers while the EB3A is on battery at 58% and 128 W and is the only station
- **THEN** the tooltip reads like `EB3A · 58% · 128 W out · about 1 h left`

#### Scenario: Two stations
- **WHEN** the tray station is the EB3A at 100% on mains and the DELTA 2 is at 82%
- **THEN** the tooltip reads like `EB3A · 100% · on mains` followed by `DELTA 2 · 82%` on the next line

#### Scenario: Charging
- **WHEN** the user hovers while the tray station is charging
- **THEN** the tooltip shows percentage, power and that it is charging, not a runtime

### Requirement: Details flyout
Left-clicking the icon SHALL open a flyout near the tray, in the current light or dark theme, showing every listed station with the tray station first. Each station SHALL show its name, glyph, percentage, remaining runtime or charging state, and current power, plus an expandable section listing everything the station reports: AC and DC input/output power, AC and DC input voltage, output on/off, net battery power, load present, model, serial number, firmware versions, and the station settings when available.

#### Scenario: Expand details
- **WHEN** the user expands a station's details section
- **THEN** every value that station reports is listed with its label and unit

#### Scenario: Two stations
- **WHEN** the flyout opens with the EB3A as tray station and the DELTA 2 listed
- **THEN** the EB3A is shown first and the DELTA 2 below it, each with live data

#### Scenario: Disconnected flyout
- **WHEN** the flyout is opened while a station has no data
- **THEN** that station shows its disconnected reason instead of stale values, and the other stations show their data

#### Scenario: Battery level only
- **WHEN** the DELTA 2 is shown from its advertisement
- **THEN** it shows the percentage marked as battery level only, the reason, and the action that unlocks live data (sign in, try again, or resume)

#### Scenario: Dismiss
- **WHEN** the user clicks elsewhere or presses Escape
- **THEN** the flyout closes

### Requirement: Station glyph
The flyout SHALL show for each station a small (about 96 px tall) vector glyph of its model, selected from the model reported by the station, whose parts reflect live data: the battery fill level and state colour on the station's screen, and each output and input group that the model's front view shows, highlighted when switched on or active. Values for groups the front view does not show (the DELTA 2's rear AC outlets and inputs) SHALL appear in the station's In/Out strip instead. Glyphs SHALL stay sharp at any display scaling and follow the theme. The EB3A and the DELTA 2 SHALL have dedicated glyphs drawn from the owner's outlines; an unrecognised model SHALL show a generic battery glyph with the reported model name.

#### Scenario: EB3A recognised
- **WHEN** the station reports model `EB3A`
- **THEN** the EB3A glyph is shown with its screen filled to the current percentage

#### Scenario: DELTA 2 recognised
- **WHEN** the station is a DELTA 2 at 82% with the AC output on at 103 W and USB off
- **THEN** the DELTA 2 glyph shows its screen filled to 82% with the USB ports inactive, and the In/Out strip shows 103 W AC out

#### Scenario: DELTA 2 USB on
- **WHEN** the DELTA 2 USB outputs are switched on
- **THEN** the USB-A and USB-C ports on the glyph are highlighted

#### Scenario: Outputs and inputs
- **WHEN** AC output is on at 128 W, DC output is off, and mains supplies 390 W
- **THEN** the AC outlets are highlighted, the DC outputs appear inactive, and the AC input is highlighted

#### Scenario: Unknown model
- **WHEN** the station reports a model without a dedicated glyph
- **THEN** a generic battery glyph and the reported model name are shown

### Requirement: Context menu and settings
Right-clicking the icon SHALL offer Open upsy (default), Pause for 30 minutes / Resume for the tray station, a Show in tray choice listing every station when more than one is listed, Add station, a Start with Windows toggle, the low-battery threshold (10-30%), Sign out of EcoFlow when signed in, and Exit, and the app SHALL persist these settings between runs. Each station in the flyout SHALL offer its own Pause / Resume, Show in tray and Forget actions.

#### Scenario: Start with Windows
- **WHEN** the user enables Start with Windows
- **THEN** the app launches at the next sign-in

#### Scenario: Choose the tray station
- **WHEN** the user picks DELTA 2 under Show in tray
- **THEN** the icon switches to the DELTA 2 at once and the choice survives a restart

#### Scenario: Single station
- **WHEN** only the EB3A is listed
- **THEN** the menu shows no Show in tray choice

#### Scenario: Exit
- **WHEN** the user chooses Exit
- **THEN** the icon is removed, every station link is released and the process ends

## ADDED Requirements

### Requirement: EcoFlow sign-in window
When the user chooses Sign in for an EcoFlow station, the app SHALL open a small window in the current theme that asks for the EcoFlow email and password, says that only the account's user ID is kept and that the password is not stored, shows progress and EcoFlow's error message, and closes on success.

#### Scenario: Sign in from the flyout
- **WHEN** the DELTA 2 shows that sign-in is needed and the user chooses Sign in
- **THEN** the sign-in window opens with focus in the email field

#### Scenario: Cancel
- **WHEN** the user closes the window without signing in
- **THEN** nothing is stored and the DELTA 2 stays in battery-level-only mode

### Requirement: Add station window
Choosing Add station SHALL show the supported stations advertising nearby that are not yet listed, refreshing while open, and SHALL add the one the user picks.

#### Scenario: Pick the DELTA 2
- **WHEN** the window lists `DELTA 2 · R33-0001` and the user picks it
- **THEN** the DELTA 2 appears in the flyout and starts connecting, or asks for EcoFlow sign-in when none is stored
