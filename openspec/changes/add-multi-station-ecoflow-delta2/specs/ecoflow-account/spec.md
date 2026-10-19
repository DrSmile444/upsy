# Spec Delta

## Purpose

Defines how upsy obtains and keeps the EcoFlow account user ID that EcoFlow stations require before they share data over Bluetooth, without keeping the user's password.

## ADDED Requirements

### Requirement: Sign in to EcoFlow
When an EcoFlow station is listed and no EcoFlow user ID is stored, the app SHALL offer to sign in with the EcoFlow account email and password, SHALL send them only to EcoFlow's login service over HTTPS, and SHALL keep only the returned user ID.

#### Scenario: Successful sign-in
- **WHEN** the user signs in with the account the DELTA 2 is bound to
- **THEN** the user ID is stored, the password is discarded, and the DELTA 2 link starts its authenticated session

#### Scenario: Wrong password
- **WHEN** EcoFlow rejects the email or password
- **THEN** the app shows EcoFlow's message, keeps nothing, and lets the user try again

#### Scenario: No internet
- **WHEN** the login service cannot be reached
- **THEN** the app says that signing in needs internet once and keeps the station in battery-level-only mode

### Requirement: Protect the stored user ID
The app SHALL store the EcoFlow user ID encrypted for the current Windows user, SHALL never write the password to disk or to the log, and SHALL never write the user ID to the log.

#### Scenario: Settings file copied to another user
- **WHEN** the settings file is read by a different Windows user
- **THEN** the user ID cannot be recovered from it

#### Scenario: Log review
- **WHEN** the user opens the log after signing in
- **THEN** it records that sign-in succeeded without the email, password or user ID

### Requirement: Works offline after sign-in
After a successful sign-in, the app SHALL connect to EcoFlow stations without contacting EcoFlow's servers.

#### Scenario: Offline restart
- **WHEN** the PC has no internet and the app restarts with a stored user ID
- **THEN** the DELTA 2 connects and shows live data

### Requirement: Rejected key guidance
When a station rejects the account key, the app SHALL stop retrying authentication for that station, SHALL explain that the station must be removed from and added again to the same account in the EcoFlow app, and SHALL retry when the user asks.

#### Scenario: Station bound before
- **WHEN** the DELTA 2 answers authentication with the wrong-key code
- **THEN** the flyout explains how to rebind it in the EcoFlow app, offers Try again, and shows the advertised battery level meanwhile

### Requirement: Sign out
The app SHALL let the user sign out of EcoFlow, which deletes the stored user ID and moves EcoFlow stations to battery-level-only mode.

#### Scenario: Sign out
- **WHEN** the user signs out
- **THEN** the user ID is deleted and the DELTA 2 shows only its advertised battery level
