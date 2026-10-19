# Tasks

## 1. Estimate

- [ ] 1.1 Add `ChargeEfficiency` to `StationProfile` (EB3A 0.75 provisional, DELTA 2 1.0); verify a catalog test reads both values
- [ ] 1.2 Add the charge side to `RuntimeEstimator` (3-minute exponential average of net charge power, 60 s warm-up, `EstimateToFull` preferring a reported `ToFull`, null when not charging, 24 h cap); verify tests:
  - 54%, 390 W in, 126 W out after a minute gives 268 × 0.46 ÷ (264 × efficiency);
  - nothing before 60 s;
  - nothing when input equals output below 100%;
  - output cycling 40 ↔ 130 W moves the estimate by less than a direct recompute would;
  - a reported `ToFull` wins.
- [ ] 1.3 In `TrayApp`, keep the host snapshot's telemetry with `ToFull` filled from `EstimateToFull` when the station does not report one, before building views and alerts; verify a `StatusText` test with an estimated `ToFull` reads "Full in about 35 min · mains 228 V" and the mains-back alert gets the time once it is ready

## 2. Charge log

- [ ] 2.1 Implement `ChargeLog` in Core (integrate net W × dt for every charging sample, skip gaps over 10 s, a summary each minute, restart after a pause of more than a minute); verify tests with a fake clock: energy over a cycling load equals the integral, no lines when not charging, a restart after a long pause
- [ ] 2.2 Write the summaries from `TrayApp` as `<name> charge: <pct>%, in <W> W, out <W> W, +<W> W net, <Wh> Wh in <min> min`; verify with a live charge that lines appear once a minute
- [ ] 2.3 Add `tools/charge_efficiency.py` (last complete charge in `upsy.log` → efficiency and net-power spread); verify on a synthetic log with a known answer

## 3. Calibration and docs

- [ ] 3.1 Owner: let the EB3A charge from about 40% to 100% with the boiler connected as usual, upsy running; verify the log holds the charge lines
- [ ] 3.2 Run the tool on that log, set the EB3A `ChargeEfficiency` to the result, and record the run (Δ%, Wh, spread) in the design; verify the estimate on the next charge matches the observed time to full within about 10%
- [ ] 3.3 Update README (time to full for the EB3A, the charge log line, the tool) and DESIGN.md if the copy changes; run the quality gate (Release build with 0 warnings, tests, format check, `openspec validate --strict`)
