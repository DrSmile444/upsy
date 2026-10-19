# Design

## Context

- `Telemetry.ToFull` exists and the DELTA 2 driver fills it from the station.
- `StatusText` already words it ("Full in about … · mains 228 V", "Charging at 61%, full in about …") and `StationView` shows a "Full in" Details row. The surfaces are the frames in `design/upsy.design.html` #multi (DELTA 2 charging), so the EB3A only has to fill the same field.
- `RuntimeEstimator` (one per station in `TrayApp.StationHost`) smooths discharge power with a 60 s exponential average, waits 10 s before showing anything, and prefers a reported `ToEmpty`.
- `PowerReading` says "charging" when net (input − output) exceeds 10 W below 100%.

Field data (see proposal):
- Net charge power was 264 W at two different loads (126 W on 2026-10-12, 44 W on 2026-10-19).
- 22% → 84% took 52 min, about 166 Wh, so about 192 W reached the battery.
- The load is a boiler cycling about 40 ↔ 130 W.
- At 92% the station sat with input = output during a thermal pause.

## Goals / Non-Goals

**Goals:**
- A steady, honest EB3A time to full that improves once one charge has been logged.
- Calibration from data the app already sees, with no extra spike or pause.

**Non-Goals:**
- Modelling constant-voltage taper.
- Explaining thermal pauses.
- Any change to DELTA 2 behaviour or to the UI copy.

## Decisions

1. **Estimate in `RuntimeEstimator`, published as `Telemetry.ToFull`.**
   - The estimator gains a charge side: an exponential average of net charge power (input − output, floored at 0), time constant 3 min, warm-up 60 s.
   - `EstimateToFull(t, p)` returns null unless `p.Charging`. It returns the reported `t.ToFull` when present. Otherwise it computes `capacity × (100 − %) / 100 / (average × efficiency)`, capped at 24 h like runtime.
   - `TrayApp` stores `t with { ToFull = estimate }` in the host's snapshot before building the view and the alerts, so `StatusText`, `StationView` and notifications need no change.
   - Rejected: computing it inside `StatusText`, which would make presentation stateful. Also rejected: a separate `ToFullEstimate` field, a second path through every view.

2. **3-minute smoothing, 60 s warm-up for charging.**
   - The boiler compressor flips the output for minutes at a time. If the charger holds a constant charge power (the two 264 W samples suggest it does), net is steady and the window costs nothing.
   - If it does not, a 3-minute average keeps the number from jumping on every compressor switch.
   - The 60 s warm-up avoids flashing a time computed from the first samples after mains returns.
   - Rejected: the 60 s discharge window, too jumpy for multi-minute load cycles.
   - Rejected: averaging since the charge started, which reacts too slowly when the station's own charge rate changes (Silent/Turbo modes, thermal limits).

3. **Per-model charge efficiency in `StationProfile`.**
   - `ChargeEfficiency` is 0.75 for the EB3A, a provisional value from the 2026-10-19 charge, replaced by the calibrated one in tasks.
   - The DELTA 2 gets 1.0; it is unused because the station reports its own value.
   - Rejected: one global factor, because chargers differ per model.

4. **`ChargeLog` in Core, written by the app.** A pure accumulator per station:
   - `Add(telemetry, now)` integrates net W × dt for every sample while charging. Gaps over 10 s are not integrated.
   - Once a minute it returns a summary: battery %, average in and out for the minute, accumulated Wh, and minutes since the start.
   - A charge interrupted for more than a minute starts over.
   - `TrayApp` writes each summary as a log line, for example `EB3A charge: 47%, in 309 W, out 88 W, +221 W net, 54.3 Wh in 15 min`.
   - Rejected: a separate CSV file. One log keeps diagnostics in one place, and the line format is stable enough to parse.

5. **`tools/charge_efficiency.py`.**
   - Parses the charge lines of one charge from `upsy.log`.
   - Prints Δ% × capacity / accumulated Wh as the efficiency, plus the net-power spread to show whether the charger held a constant power under the load swings.
   - Rejected: computing the factor inside the app automatically. One deliberate calibration is easier to check, and a wrong factor would silently skew every later estimate.

## Risks / Trade-offs

- [The provisional 0.75 is wrong] → The number is labelled "about". The calibration task replaces the value, and the tool shows the spread so a bad run is visible.
- [Taper near 100% makes the last minutes slower than estimated] → Because the estimate uses current smoothed power, it lengthens as power drops. A taper model stays out of scope until the log shows one.
- [Thermal pause] → Net ≤ 10 W is "not charging", so no time is shown and the estimate restarts its warm-up when charging resumes.
- [Log growth] → One line a minute while charging, about 60–90 lines per charge, inside the existing 512 KB rotation.
