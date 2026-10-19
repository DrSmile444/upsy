# Design

## Context

- `Telemetry.ToFull` and `Telemetry.ToEmpty` are filled only by the DELTA 2 driver, from the station.
- `RuntimeEstimator` (one per station in `TrayApp.StationHost`) smooths discharge power with a 60 s exponential average, waits 10 s, and prefers a reported `ToEmpty`. Its result is a bare `TimeSpan?`, so `StatusText` cannot tell a station value from an estimate. It words both with "about" and rounding (DESIGN.md copy rule "Runtime always about").
- `StatusText` already has slots for time to full ("Full in … · mains 228 V"), the mains-back alert ("Charging at 61%, full in …") and the "Full in" / "Empty in" Details rows in `StationView`. These are the frames in `design/upsy.design.html` #multi; the EB3A battery frames are in the first flyout boards.
- `PowerReading` says "charging" when net (input − output) exceeds 10 W below 100%.

Field data (see proposal):
- net charge 264 W at two loads;
- 22% → 84% in 52 min (about 166 Wh, so about 192 W into the battery);
- a boiler load cycling about 40 ↔ 130 W;
- a thermal pause at 92% with input = output.

## Goals / Non-Goals

**Goals:**
- An EB3A time to full that is steady and honest about being an estimate.
- Station values worded exactly.
- Calibration of both EB3A factors from one logged cycle, with no extra spike or pause.

**Non-Goals:**
- Modelling the charge taper.
- Explaining thermal pauses.
- Changing what the DELTA 2 reports.

## Decisions

1. **`TimeEstimate(TimeSpan Value, bool FromStation)`** is what the estimator returns for both directions:
   - `EstimateToEmpty(t, p)` keeps today's logic and marks a reported `ToEmpty` as `FromStation`;
   - `EstimateToFull(t, p)` uses a reported `ToFull` as `FromStation`, otherwise the charge-side estimate.

   `StatusText.Build` and `StatusText.Alert` take these instead of a bare runtime, and `StationSection` carries them for the Details rows.
   - Rejected: writing the estimate into `Telemetry.ToFull`, because the source would be lost and the wording could not differ.
   - Rejected: a flag on `Telemetry`, because telemetry is what the station said, not what upsy inferred.

2. **Charge side of `RuntimeEstimator`.**
   - An exponential average of net charge power (input − output, floored at 0) with a 3-minute time constant and a 60 s warm-up.
   - The result is `capacity × (100 − %) / 100 / (average × efficiency)`, null unless `p.Charging`, capped at 24 h.
   - The warm-up restarts after charging stops (net ≤ 10 W) and starts again.
   - 3 minutes rides out boiler compressor cycles if the charger does not hold a constant power; the two 264 W samples suggest it does, in which case the window costs nothing.
   - Rejected: the 60 s discharge window, too jumpy for multi-minute cycles.
   - Rejected: averaging since the charge started, which reacts too slowly to Silent/Turbo or thermal changes.

3. **Wording by source** (DESIGN.md copy rules and canvas updated first):

   | | Station (`FromStation`) | Estimate |
   |---|---|---|
   | Headline (battery) | `7 h 11 min left` | `About 1 h left` |
   | Line under it (battery) | `At the current 103 W load · from the station` | `Estimated at the current 128 W load` |
   | Line under it (charging) | `Full in 1 h 13 min · mains 233 V` | `Full in about 35 min, estimated · mains 228 V` |
   | Details row | `1 h 13 min (station)` | `About 35 min (estimated)` |
   | Tooltip | `… · 7 h 11 min left` | `… · about 1 h left (estimated)` |
   | Notification | `…, 7 h 11 min at 103 W.` | `…, about 1 h 40 min at 128 W (estimated).` |

   - Station values are formatted exactly, to the minute ("1 h 13 min", "45 min"); estimates keep "about" and the 5/10-minute rounding. Both cap at "more than 24 h".
   - Low and critical headlines keep "Low · about 20 min left"; the line under them already carries the instruction, so "estimated" goes into the tooltip and the notification.
   - Rejected: "~35 min" and "est.", which read as abbreviations in a plain-language UI.

4. **Per-model factors in `StationProfile`.**
   - `ChargeEfficiency` (EB3A 0.75, provisional; 0.93 after calibration) joins the existing `UsableFactor`, which moves from a constant into the profile (EB3A 0.85, provisional; confirmed by calibration).
   - The DELTA 2 gets 1.0 for both; they are unused because it reports its own times.

5. **`EnergyLog` in Core, written by the app.** A pure accumulator per station:
   - Direction is *charge* (`p.Charging`) or *discharge* (on battery with a load). Anything else is idle.
   - Every sample integrates net power × dt into the current run; gaps over 10 s are skipped.
   - Once a minute it returns a summary: direction, battery %, average in and out for the minute, accumulated Wh, minutes since the start.
   - Idle for more than a minute, or a change of direction, starts a new run.
   - `TrayApp` writes summaries as log lines, for example `EB3A discharge: 72%, in 0 W, out 88 W, -88 W net, 23.1 Wh in 15 min` and `EB3A charge: 47%, in 309 W, out 88 W, +221 W net, 54.3 Wh in 15 min`.
   - Rejected: a separate CSV file. One log keeps diagnostics in one place, and the line format is stable to parse.

6. **`tools/energy_calibration.py`.** Reads the last complete discharge and charge runs from `upsy.log` and prints:
   - *usable factor* = Wh out / (Δ% × capacity), for the runtime estimate;
   - *charge efficiency* = (Δ% × capacity) / Wh in;
   - the net-power spread of each run.
   - Rejected: computing the factors in the app automatically. One deliberate calibration is easier to check, and a wrong factor would silently skew every later estimate.

## Risks / Trade-offs

- [Provisional factors are wrong] → Every EB3A time says "estimated". The calibration task replaces both factors, and the tool shows the spread so a bad run is visible.
- [Taper near 100% slows the last minutes] → The estimate uses current smoothed power, so it lengthens as power drops.
- [Thermal pause] → Net ≤ 10 W counts as not charging: no time is shown and the warm-up restarts.
- [Log growth] → One line a minute while charging or discharging, about 200 lines per full cycle, inside the 512 KB rotation.
- [Exact station values change every minute] → They are the station's own numbers and change once a minute, which is fine at flyout glance speed.

## Calibration (2026-10-19)

One EB3A cycle with the boiler as usual, logged by `EnergyLog` and read by `tools/energy_calibration.py`:

| Run | Δ% | Energy | Net power | Factor |
|---|---|---|---|---|
| Discharge | 99% → 35% in 118 min | 145.9 Wh out | −74 W mean, −95 … −49 W, sd 6 W | usable factor **0.851** |
| Charge, constant power | 36% → 86% in 33 min | 144.1 Wh in | +264 W, sd 1 W | charge efficiency **0.930** |
| Charge, whole run | 36% → 94% in 40 min | 166.1 Wh in | +244 W mean, +150 … +265 W | charge efficiency 0.936 |

- `UsableFactor` stays 0.85; `ChargeEfficiency` goes from 0.75 to **0.93**.
- The charger holds 264 W net up to 86%, then drops to about 155 W (the taper). The taper is slower power, not a loss: 87% → 94% gives the same efficiency within the whole-percent resolution, so the factor comes from the constant-power part and the estimate lengthens on its own when power drops.
- Check against the observed times: the discharge predicts 64% × 268 Wh × 0.85 ÷ 74 W = 118 min (observed 118 min); the charge predicts 50% × 268 Wh ÷ (264 W × 0.93) = 33 min to 86% (observed 33 min). With the provisional 0.75 the charge estimate was 41 min, 24% long.
- The log stopped at 94% (net ≤ 10 W or the end of the run); the last few percent to 100% were not measured.
