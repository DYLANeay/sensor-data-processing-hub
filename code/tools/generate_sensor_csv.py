"""Generate simulated sensor CSV data for the Sensor Data Processing Hub.

Each tick emits one temperature (C), co2 (ppm) and humidity (%) row:

    timestamp,sensor_id,type,value,unit

Timestamps are ISO 8601 UTC without offset, e.g. 2026-11-19T08:00:00.
Values are baseline + daily sine cycle + gaussian noise, clamped to plausible
indoor ranges, with a few intentional threshold spikes and (by default) three
malformed rows so the C# parser's error path is visible.

A fixed --seed makes the output byte-identical across runs.

Usage:
    uv run code/tools/generate_sensor_csv.py --out code/data/room-a.csv \
        --sensor-id room-a --seed 42
"""

import argparse
import csv
import math
import random
import sys
from datetime import datetime, timedelta

HEADER = ["timestamp", "sensor_id", "type", "value", "unit"]


def clamp(value, low, high):
    return max(low, min(high, value))


def temperature(dt, rng):
    """~21 C with a slow daily cycle, clamped to a plausible 18-26 C."""
    daily = 1.5 * math.sin(2 * math.pi * (dt.hour + dt.minute / 60) / 24)
    return clamp(21.0 + daily + rng.gauss(0, 0.4), 18.0, 26.0)


def humidity(dt, rng):
    """~45 % with a slow daily cycle, clamped to a plausible 30-60 %."""
    daily = 5.0 * math.sin(2 * math.pi * (dt.hour + dt.minute / 60) / 24)
    return clamp(45.0 + daily + rng.gauss(0, 1.5), 30.0, 60.0)


def co2(dt, rng):
    """~450 ppm at night, ~850 ppm during occupied hours (09:00-17:00)."""
    base = 850.0 if 9 <= dt.hour < 17 else 450.0
    hourly = 60.0 * math.sin(2 * math.pi * dt.minute / 60)
    return clamp(base + hourly + rng.gauss(0, 25.0), 350.0, 1100.0)


def insert_malformed(rows):
    """Insert three malformed rows at fixed positions: empty value, NaN,
    and a truncated 3-column row, so the C# error path is visible."""
    n = len(rows)
    specs = [
        (n // 10, "temperature", ["", "C"]),
        (n // 2, "humidity", ["NaN", "%"]),
        (4 * n // 5, "co2", None),  # truncated: no value/unit columns
    ]
    for pos, mtype, rest in sorted(specs, key=lambda s: s[0], reverse=True):
        ts, sid = rows[pos][0], rows[pos][1]
        row = [ts, sid, mtype] if rest is None else [ts, sid, mtype, *rest]
        rows.insert(pos, row)


def write_csv(out, rows):
    writer = csv.writer(out, lineterminator="\n")
    writer.writerow(HEADER)
    writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(
        description="Generate seeded simulated sensor CSV data."
    )
    parser.add_argument("--out", help="output CSV file (default: stdout)")
    parser.add_argument("--sensor-id", default="room-a")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--start", default="2026-11-19T08:00:00")
    parser.add_argument("--duration-minutes", type=int, default=120)
    parser.add_argument("--interval-seconds", type=int, default=10)
    parser.add_argument("--no-errors", action="store_true",
                        help="disable malformed rows")
    args = parser.parse_args()

    rng = random.Random(args.seed)
    start = datetime.fromisoformat(args.start)
    ticks = args.duration_minutes * 60 // args.interval_seconds

    # Short spikes so threshold alerts fire in the demo.
    temp_spike_ticks = {ticks // 3, ticks // 3 + 1}  # > 28 C for ~20 s
    co2_spike_ticks = {2 * ticks // 3}               # > 1200 ppm for 1 tick

    rows = []
    for i in range(ticks):
        dt = start + timedelta(seconds=i * args.interval_seconds)
        ts = dt.strftime("%Y-%m-%dT%H:%M:%S")

        temp = temperature(dt, rng)
        if i in temp_spike_ticks:
            temp += 8.0
        level = co2(dt, rng)
        if i in co2_spike_ticks:
            level = 1250.0 + rng.gauss(0, 20.0)

        rows.append([ts, args.sensor_id, "temperature", f"{temp:.1f}", "C"])
        rows.append([ts, args.sensor_id, "co2", f"{level:.0f}", "ppm"])
        rows.append([ts, args.sensor_id, "humidity", f"{humidity(dt, rng):.1f}", "%"])

    if not args.no_errors:
        insert_malformed(rows)

    if args.out:
        with open(args.out, "w", newline="") as f:
            write_csv(f, rows)
    else:
        write_csv(sys.stdout, rows)


if __name__ == "__main__":
    main()
