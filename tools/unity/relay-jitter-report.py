"""Summarize Relay native bot and frozen-frame capture files."""
import argparse
import csv
import math
import re
from collections import Counter, defaultdict
from pathlib import Path
from statistics import mean, median

TIMES = ("wall_ms", "cpu_ms", "main_ms", "render_ms", "gpu_ms", "present_wait_ms")


def load(path):
    with path.open(newline="") as file:
        rows = list(csv.DictReader(file))
    if len(rows) != 240:
        raise ValueError(f"{path}: expected 240 samples")
    frames = [{key: float(value) for key, value in row.items()} for row in rows[12:]
              if float(row["cpu_ms"]) > 0]
    if len(frames) < 214 or not any(row["draws"] > 0 for row in frames):
        raise ValueError(f"{path}: too few frame samples or missing render counters")
    if not all(row["focused"] == 1 for row in frames):
        return []
    if path.stem.startswith("live-") and not all(row["players"] == 12 for row in frames):
        raise ValueError(f"{path}: unexpected bot-match population")
    return frames


def percentile(frames, key, fraction):
    values = sorted(row[key] for row in frames)
    return values[math.ceil(len(values) * fraction) - 1]


def summarize(folders):
    collision = (folders[0] / "live-start-collision.txt").read_bytes()
    for folder in folders:
        signatures = sorted(folder.glob("*-collision.txt"))
        if len(signatures) < 2 or any(file.read_bytes() != collision for file in signatures):
            raise ValueError(f"{folder}: collision fingerprints differ or are missing")
    print(f"{len(collision.splitlines())} matching active Relay colliders.")
    for folder in folders:
        print(f"\n## {folder.name}\n\n{(folder / 'setup.txt').read_text().strip()}\n")
        print("| Block | Valid frames | Wall median / p95 ms | CPU median ms | Main median ms | Render median ms | GPU median ms | Present wait median ms | Draws mean | Triangles mean |")
        print("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
        groups = defaultdict(list)
        excluded = []
        for file in sorted(folder.glob("*.csv")):
            frames = load(file)
            if not frames:
                excluded.append(file.stem)
                continue
            key = re.sub(r"-(\d+)(?=-|$)","",file.stem)
            groups[key].extend(frames)
            metrics = [f"{median(row[k] for row in frames):.2f}" for k in TIMES]
            metrics[0] += f" / {percentile(frames,'wall_ms',.95):.2f}"
            metrics.extend(f"{mean(row[k] for row in frames):.0f}" for k in ("draws","triangles"))
            print("| "+" | ".join((file.stem, str(len(frames)), *metrics))+" |")
        if excluded:
            print("\nExcluded for losing window focus: "+", ".join(excluded)+".")
        print()
        print("| Pooled blocks | Frames | Wall median / p95 ms | Render median ms | Draw mean | Draw min–max | Above 20 ms | Reported sync |")
        print("|---|---:|---:|---:|---:|---:|---:|---:|")
        for key, frames in sorted(groups.items()):
            sync = ", ".join(str(int(v)) for v in sorted({r["sync_interval"] for r in frames if "sync_interval" in r})) or "n/a"
            late = sum(r["wall_ms"]>20 for r in frames)
            print(f"| {key} | {len(frames)} | {median(r['wall_ms'] for r in frames):.2f} / "
                  f"{percentile(frames,'wall_ms',.95):.2f} | {median(r['render_ms'] for r in frames):.2f} | "
                  f"{mean(r['draws'] for r in frames):.0f} | {min(r['draws'] for r in frames):.0f}–"
                  f"{max(r['draws'] for r in frames):.0f} | {late} | {sync} |")
        for key, frames in sorted(groups.items()):
            if not key.startswith("frozen-"):
                continue
            shape = Counter((row["draws"],row["triangles"]) for row in frames).most_common(1)[0][0]
            stable = sorted((r for r in frames if (r["draws"],r["triangles"]) == shape),
                            key=lambda row: row["cpu_ms"])
            decile = len(stable) // 10
            if not decile:
                continue
            print(f"\n{key}, {len(stable)} same-work frames ({shape[0]:.0f} draws, {shape[1]:.0f} triangles).")
            for label, rows in (("Fastest CPU decile", stable[:decile]), ("Slowest CPU decile", stable[-decile:])):
                stats = ", ".join(f"{field} {median(r[field] for r in rows):.2f}" for field in TIMES[1:])
                print(f"- {label} medians in ms: {stats}.")


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runs",nargs="+",type=Path)
    summarize(parser.parse_args().runs)
