# Proposal

## Why

When the DELTA 2 charges, upsy shows "Full in about 1 h 10 min", because the station reports that time itself. The EB3A reports no such value, so while it charges upsy shows only the charging power. The owner wants the same answer for the EB3A. upsy can derive it from the capacity, the battery percentage and the power going into the battery.

How exact that number is depends on how much of the measured input really ends up in the battery:
- On 2026-10-19 the EB3A went from 22% to 84% in 52 minutes. Two samples showed 264 W net (input minus output), and the same 264 W appeared on 2026-10-12 at a different load. This suggests the charger holds a constant charge power and the AC load is passed through.
- Those numbers imply that only about 75% of the measured net power reached the battery.
- The EB3A also powers a boiler whose compressor cycles between about 40 and 130 W. Two point samples cannot separate charger losses from load swings.

So the efficiency has to be measured over a whole charge.

## What Changes

- **Estimate time to full** for stations that do not report it (the EB3A):
  - remaining energy = nominal capacity × (100 − battery %) / 100;
  - net charge power = input minus output, smoothed over a few minutes so compressor cycles do not jump the number;
  - divide by a per-model charge efficiency.
- **Show it everywhere the DELTA 2's value shows:** the flyout detail line ("Full in about 40 min · mains 228 V"), the Details row, and the mains-back notification once the estimate is ready.
- **Stations that report their own time to full** (DELTA 2) keep using it unchanged.
- **No time while not really charging:** net power at or below 10 W, as during the EB3A's thermal pause, shows no time to full.
- **A charge log for calibration.** While a station charges, upsy writes one line a minute to its log:
  - battery %;
  - average input and output for that minute;
  - charge energy accumulated since the charge started, integrated over every sample.

  A small tool turns a logged charge into the efficiency factor.
- **Hardware task:** log one EB3A charge from about 40% to 100% under the real boiler load, then set the EB3A factor from it.

Out of scope: a charge-curve model (constant-voltage taper near 100%); time to full from DC/solar beyond the same net-power rule; changing what the DELTA 2 shows; explaining the EB3A's thermal pause (no known register).

Later: calibrate the 0.85 discharge factor from a logged discharge, using the same log; a taper correction if the logged charge shows one.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
- `power-state`: "Time to full" changes from "reported values only; others show the charging power" to "reported value, else an estimate from capacity, battery % and smoothed net charge power with a per-model efficiency". Adds a requirement for the charge log used to calibrate it.

## Impact

- **Code.** `Upsy.Core/Power`:
  - a charge-side estimate next to the discharge one in `RuntimeEstimator`;
  - a per-model charge efficiency in `StationProfile`;
  - a pure `ChargeLog` accumulator.

  `Upsy/Tray/TrayApp`: fill `Telemetry.ToFull` from the estimate when the station does not report it, and write the charge log lines. Presentation and alerts already show `ToFull` and need no change.
- **Tools.** `tools/charge_efficiency.py` reads `upsy.log` and prints the factor for a logged charge.
- **No UI or design change.** The surfaces and copy already exist for the DELTA 2; the EB3A fills the same slots.
- **Risks:**
  - the factor stays approximate until the calibration charge is logged;
  - if the charger does not hold a constant power under load swings, the smoothing window decides how steady the number feels.
