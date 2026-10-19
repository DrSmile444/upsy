"""Work out a station's capacity factors from the energy lines in upsy.log.

While a station charges or discharges into a load, upsy logs one line a minute, for example:

  2026-10-20 10:15:02 INFO EB3A discharge: 72%, in 0 W, out 88 W, -88 W net, 23.1 Wh in 15 min
  2026-10-20 13:40:07 INFO EB3A charge: 47%, in 309 W, out 88 W, +221 W net, 54.3 Wh in 15 min

The energy is integrated over every 2 s sample, so a load that cycles (a boiler compressor) is counted exactly.
From the last discharge and the last charge of a station this prints:

  usable factor     = energy out / (percent lost x capacity)    -> StationProfile.UsableFactor (runtime)
  charge efficiency = (percent gained x capacity) / energy in   -> StationProfile.ChargeEfficiency (time to full)

Run: python tools/energy_calibration.py [--station EB3A] [--log %LOCALAPPDATA%\\upsy\\upsy.log]
"""
import argparse
import os
import re
import statistics
from pathlib import Path

CAPACITY_WH = {"EB3A": 268, "DELTA 2": 1024}
LINE = re.compile(
    r"^(?P<ts>\S+ \S+) INFO (?P<name>.+?) (?P<dir>charge|discharge): (?P<pct>\d+)%, in (?P<inw>\d+) W, "
    r"out (?P<outw>\d+) W, (?P<net>[+-]?\d+) W net, (?P<wh>[\d.]+) Wh in (?P<min>\d+) min$"
)
# The station reports whole percent; a short run gives a coarse answer.
MIN_PERCENT = 20


def read_runs(lines, station):
    """Group consecutive lines of one direction into runs; a run restarts when its minute counter goes back."""
    runs = []
    for line in lines:
        m = LINE.match(line.strip())
        if not m or m["name"] != station:
            continue
        row = {k: m[k] for k in ("ts", "dir")} | {
            "pct": int(m["pct"]), "inw": int(m["inw"]), "outw": int(m["outw"]),
            "net": int(m["net"]), "wh": float(m["wh"]), "min": int(m["min"]),
        }
        last = runs[-1][-1] if runs else None
        if last is None or last["dir"] != row["dir"] or row["min"] <= last["min"]:
            runs.append([row])
        else:
            runs[-1].append(row)
    return runs


def summarise(run, capacity):
    first, last = run[0], run[-1]
    percent = abs(last["pct"] - first["pct"])
    energy = last["wh"] - first["wh"]
    nets = [r["net"] for r in run]
    share = percent / 100 * capacity
    if run[0]["dir"] == "discharge":
        factor = energy / share if share else None
        name = "usable factor"
    else:
        factor = share / energy if energy else None
        name = "charge efficiency"
    return {
        "name": name, "factor": factor, "percent": percent, "energy": energy,
        "from": first["pct"], "to": last["pct"], "minutes": last["min"] - first["min"],
        "start": first["ts"], "net_min": min(nets), "net_max": max(nets),
        "net_mean": statistics.fmean(nets), "net_sd": statistics.pstdev(nets),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--station", default="EB3A")
    parser.add_argument("--log", default=os.path.join(os.environ.get("LOCALAPPDATA", ""), "upsy", "upsy.log"))
    parser.add_argument("--capacity", type=float, help="nominal Wh (default: known value for the station)")
    args = parser.parse_args()

    capacity = args.capacity or CAPACITY_WH.get(args.station)
    if not capacity:
        raise SystemExit(f"unknown capacity for {args.station}; pass --capacity")

    paths = [Path(args.log + ".1"), Path(args.log)]  # the rotated part first
    lines = [line for p in paths if p.exists() for line in p.read_text(encoding="utf-8").splitlines()]
    runs = read_runs(lines, args.station)
    if not runs:
        raise SystemExit(f"no energy lines for {args.station} in {args.log}")

    for direction in ("discharge", "charge"):
        last = next((r for r in reversed(runs) if r[0]["dir"] == direction and len(r) > 1), None)
        if last is None:
            print(f"{direction}: no complete run logged")
            continue
        s = summarise(last, capacity)
        warn = "  (short run: the answer is coarse)" if s["percent"] < MIN_PERCENT else ""
        print(f"{direction}: {s['from']}% -> {s['to']}% in {s['minutes']} min from {s['start']}, {s['energy']:.1f} Wh{warn}")
        print(f"  net power {s['net_mean']:+.0f} W mean, {s['net_min']:+d} .. {s['net_max']:+d} W, sd {s['net_sd']:.0f} W")
        print(f"  {s['name']}: {s['factor']:.3f}" if s["factor"] else f"  {s['name']}: not enough change to compute")


if __name__ == "__main__":
    main()
