# Proposal

## Why

When the DELTA 2 charges, upsy shows how long until it is full, because the station reports that time itself. The EB3A reports no such value, so while it charges upsy shows only the charging power. The owner wants the same answer for the EB3A, derived from the capacity, the battery percentage and the power going into the battery. They also want the wording to say whether a time is the station's own value or upsy's estimate.

How exact the EB3A numbers can be depends on two factors that have never been measured on this unit: how much of the measured net input ends up in the battery, and how much of the nominal 268 Wh comes out.
- On 2026-10-19 the EB3A went from 22% to 84% in 52 minutes. Two samples showed 264 W net (input minus output), and the same 264 W appeared on 2026-10-12 at a different load. That suggests the charger holds a constant charge power and passes the AC load through, and that only about 75% of the measured net reached the battery.
- The EB3A powers a boiler whose compressor cycles between about 40 and 130 W, so point samples cannot separate charger losses from load swings.

Both factors therefore have to be measured over a whole discharge and charge.

## What Changes

- **Estimate time to full** for stations that do not report it (the EB3A):
  - remaining energy = nominal capacity × (100 − battery %) / 100;
  - net charge power = input − output, smoothed over a few minutes;
  - divide by a per-model charge efficiency.

  It shows wherever the DELTA 2's value shows. With net at or below 10 W (a thermal pause, for example) no time is shown.
- **Say where a time comes from**, for time to full and for runtime on battery:
  - *Station values* (DELTA 2) are shown as reported, to the minute: "7 h 11 min left", "Full in 1 h 13 min", Details "(station)".
  - *upsy's estimates* (EB3A) keep "about" and the rounding, and are marked "estimated" in the line under the status, the Details row, the tooltip and notifications.

  This changes the copy, so the design canvas and DESIGN.md are updated first.
- **An energy log for calibration.** While a station charges, or discharges into a load on battery, upsy writes one line a minute:
  - direction and battery %;
  - average input and output for that minute;
  - energy accumulated since the charge or discharge started, integrated over every sample.

  `tools/energy_calibration.py` turns a logged discharge into the usable-energy factor and a logged charge into the charge efficiency.
- **Hardware task:** let the EB3A run on battery from 100% to about 40% and charge back to 100% under the real boiler load, then set both EB3A factors from the log.
- DELTA 2 numbers are unchanged; only their wording becomes exact.

Out of scope: a charge-curve model (constant-voltage taper); explaining the EB3A's thermal pause (no known register); a time to full from the DELTA 2's own data.

Later: a taper correction if the logged charge shows one; logging per-port DC power.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `power-state`: "Remaining runtime on battery" and "Time to full" say where the time comes from. "Time to full" estimates when the station does not report it. Adds an energy log requirement for calibration.
- `tray-ui`: "Runtime wording" distinguishes station values (as reported, to the minute) from estimates ("about", rounded, marked estimated). "Tooltip summary" marks estimates.

## Impact

- **Code.**
  - `Upsy.Core/Power`: a charge side in `RuntimeEstimator`, a `TimeEstimate` (value plus source), and a pure `EnergyLog`.
  - `StationProfile.ChargeEfficiency`.
  - `Upsy.Core/Presentation/StatusText`: wording by source.
  - `Upsy/Tray/TrayApp` and `StationView`: pass estimates, write log lines, Details suffix.
- **Design.** `design/upsy.design.html` frames that show times (EB3A battery and charging, DELTA 2 frames, tooltip, notifications) and the DESIGN.md copy rules.
- **Tools.** `tools/energy_calibration.py`.
- **Risks:**
  - the provisional factors stay approximate until the calibration cycle is logged;
  - the smoothing window decides how steady the number feels if the charger does not hold a constant power under load swings.
