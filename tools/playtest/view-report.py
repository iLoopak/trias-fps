#!/usr/bin/env python3
"""Summarise view-run.sh frame logs: per-phase crosshair and camera statistics plus frame pacing.

    python3 tools/playtest/view-report.py <out-dir> [label ...]

Windows with more than half of their frames unfocused are left out of the frame-time summary, because
macOS throttles an unfocused player. Per-phase values only exist while the pilot is alive and the view is
captured, so they are not affected by focus.
"""
import re
import sys
from pathlib import Path

FRAME = re.compile(r"^\w+ frames=(\d+) median_ms=([\d.]+) p95_ms=([\d.]+) p99_ms=([\d.]+) max_ms=([\d.]+) .*?unfocused=(\d+).*?spikes25=(\d+).*?local_judder=(-?[\d.]+)% remote_judder=(-?[\d.]+)%")
VIEW = re.compile(r"^  view (\w+) frames=(\d+) reticle_off_px mean=([\d.]+) p95=([\d.]+) max=([\d.]+) reticle_jump_px p50=([\d.]+) p95=([\d.]+) max=([\d.]+) eye_ahead_cm p50=([\d.]+) p95=([\d.]+) roll_max=([\d.]+) fov=([\d.]+)-([\d.]+)")
CORR = re.compile(r"^  corrections count=(\d+) mean_cm=([\d.]+) max_cm=([\d.]+)")
GPU = re.compile(r"^  gpu_ms median=([\d.]+) p95=([\d.]+)")
PHASES = ["still", "rotate", "pitch", "strafe", "track", "run"]


def summarise(path):
    frames, views, corr, gpu = [], {}, [0, 0.0, 0.0], []
    for line in path.read_text().splitlines():
        if m := FRAME.match(line):
            n, med, p95, p99, mx, unf, spikes, lj, rj = m.groups()
            if int(unf) <= int(n) / 2 and int(n) >= 300:
                frames.append((int(n), float(med), float(p95), float(p99), float(mx), int(spikes), float(lj)))
        elif m := VIEW.match(line):
            phase, n = m.group(1), int(m.group(2))
            v = [float(x) for x in m.groups()[2:]]
            agg = views.setdefault(phase, {"n": 0, "mean": 0, "p95": 0, "max": 0, "jump95": 0, "jumpmax": 0, "ahead": 0, "ahead95": 0, "roll": 0, "fov": 0})
            agg["mean"] += v[0] * n; agg["p95"] += v[1] * n; agg["max"] = max(agg["max"], v[2])
            agg["jump95"] += v[4] * n; agg["jumpmax"] = max(agg["jumpmax"], v[5])
            agg["ahead"] += v[6] * n; agg["ahead95"] += v[7] * n
            agg["roll"] = max(agg["roll"], v[8]); agg["fov"] = max(agg["fov"], v[10] - v[9])
            agg["n"] += n
        elif m := CORR.match(line):
            c = int(m.group(1)); corr[0] += c; corr[1] += float(m.group(2)) * c; corr[2] = max(corr[2], float(m.group(3)))
        elif m := GPU.match(line):
            gpu.append(float(m.group(1)))
    return frames, views, corr, gpu


def main():
    out = Path(sys.argv[1])
    labels = sys.argv[2:] or sorted(p.name for p in out.iterdir() if (p / "frames.log").exists())
    print("| run | frames | median ms | p95 ms | p99 ms (worst window) | spikes >25 ms | 1% low fps | local judder | GPU median ms | corrections (mean / max cm) |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    tables = {}
    for label in labels:
        frames, views, corr, gpu = summarise(out / label / "frames.log")
        tables[label] = views
        if frames:
            total = sum(f[0] for f in frames)
            med = sum(f[1] * f[0] for f in frames) / total
            p95 = sum(f[2] * f[0] for f in frames) / total
            p99 = max(f[3] for f in frames)
            spikes = sum(f[5] for f in frames)
            judder = [f[6] for f in frames if f[6] >= 0]
            low = 1000 / (sum(f[3] * f[0] for f in frames) / total)
            print(f"| {label} | {total} | {med:.2f} | {p95:.2f} | {p99:.2f} | {spikes} | {low:.0f} | {sum(judder) / max(1, len(judder)):.1f}% | {sum(gpu) / max(1, len(gpu)):.1f} | {corr[1] / max(1, corr[0]):.1f} / {corr[2]:.1f} |")
        else:
            print(f"| {label} | 0 focused windows | | | | | | | | |")
    print()
    print("Crosshair offset from screen centre and frame-to-frame jump (screen px), eye lead of the latest prediction over the shown eye (cm), max roll (deg), FOV span (deg):")
    print()
    print("| run | phase | frames | offset mean | offset p95 | offset max | jump p95 | jump max | eye lead p50 | roll max | FOV span |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for label, views in tables.items():
        for phase in PHASES:
            if phase not in views:
                continue
            a = views[phase]; n = a["n"]
            print(f"| {label} | {phase} | {n} | {a['mean'] / n:.1f} | {a['p95'] / n:.1f} | {a['max']:.1f} | {a['jump95'] / n:.1f} | {a['jumpmax']:.1f} | {a['ahead'] / n:.1f} | {a['roll']:.2f} | {a['fov']:.1f} |")


if __name__ == "__main__":
    main()
