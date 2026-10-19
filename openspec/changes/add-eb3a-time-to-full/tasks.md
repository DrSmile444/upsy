# Tasks

## 0. Design

- [x] 0.1 Update `design/upsy.design.html` and DESIGN.md "Copy rules" for station values versus estimates (decision 3): EB3A battery/low/charging frames, DELTA 2 frames in #multi, tooltip and notification boards; verify by rendering both themes and running the impeccable detector (`[]`)

## 1. Estimate and wording

- [x] 1.1 Move `UsableFactor` into `StationProfile` and add `ChargeEfficiency` (EB3A 0.85 / 0.75 provisional, DELTA 2 1.0 / 1.0); verify a catalog test reads them and existing runtime tests still pass
- [x] 1.2 Add `TimeEstimate` and the charge side of `RuntimeEstimator` (3-minute average of net charge power, 60 s warm-up, reported `ToFull` wins as a station value, null when not charging, 24 h cap); `EstimateToEmpty` marks a reported `ToEmpty` as a station value. Verify tests:
  - 54%, 390 W in, 126 W out after a minute gives 268 × 0.46 ÷ (264 × efficiency);
  - nothing before 60 s;
  - nothing when input equals output below 100%;
  - a 40 ↔ 130 W output cycle moves the estimate less than a direct recompute would;
  - reported values come back as station values.
- [x] 1.3 Word times by source in `StatusText` (headline, line under it, tooltip, alerts) and in the `StationView` Details rows ("(station)" / "(estimated)"); verify `StatusText` tests for every row of decision 3, the exact formatting ("1 h 13 min", "45 min") and the existing rounding tests
- [x] 1.4 Pass both estimates from `TrayApp` (views, alerts, flyout sections) and update `--render` previews; verify renders of EB3A battery, EB3A charging and DELTA 2 against the updated canvas frames

## 2. Energy log

- [x] 2.1 Implement `EnergyLog` in Core (charge and discharge runs, every sample integrated, gaps over 10 s skipped, one summary a minute, new run after a minute idle or a change of direction); verify tests with a fake clock: energy over a cycling load equals the integral, idle writes nothing, direction change and long gaps start new runs
- [x] 2.2 Write the summaries from `TrayApp` as `<name> <charge|discharge>: <pct>%, in <W> W, out <W> W, <±W> W net, <Wh> Wh in <min> min`; verify with the running EB3A that discharge or charge lines appear once a minute
- [x] 2.3 Add `tools/energy_calibration.py` (last complete discharge and charge runs → usable factor, charge efficiency, net spread); verify on a synthetic log with a known answer

## 3. Calibration and docs

- [x] 3.1 Owner: run the EB3A on battery from 100% to about 40% with the boiler as usual, then charge it back to 100%, upsy running; verify the log holds both runs
- [x] 3.2 Run the tool, set the EB3A `UsableFactor` and `ChargeEfficiency`, and record the run (Δ%, Wh, spread) in this design; verify the next estimates match the observed times within about 10%
- [x] 3.3 Update README (EB3A time to full, station versus estimated wording, the energy log and the tool), install the new build, and run the quality gate (Release build with 0 warnings, tests, format check, `openspec validate --strict`)
