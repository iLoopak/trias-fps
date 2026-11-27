"""Compare fixed Relay runs and fail if their active collision fingerprints differ."""
import argparse
import csv
from math import ceil
from pathlib import Path
from statistics import median

VIEWS = ("shaft", "bridge", "hut-entry", "hut-interior")
COLUMNS = ("wall_ms", "cpu_ms", "main_ms", "render_ms", "gpu_ms", "draws", "setpass", "triangles")


def profile(path):
    with path.open(newline="") as file:
        samples = list(csv.DictReader(file))
    if len(samples) != 300:
        raise ValueError(f"{path}: expected 300 measured frames")
    measured = [sample for sample in samples[10:] if float(sample["draws"]) > 0]
    if len(measured) < 275:
        raise ValueError(f"{path}: only {len(measured)} rendered samples")
    metrics = {}
    for column in COLUMNS:
        values = sorted(float(sample[column]) for sample in measured if float(sample[column]) > 0)
        metrics[column] = (median(values), values[ceil(.95 * len(values)) - 1]) if values else (0, 0)
    if metrics["draws"][0] <= 0 or metrics["gpu_ms"][0] <= 0:
        raise ValueError(f"{path}: missing render counters or GPU timing")
    return metrics


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runs", type=Path, nargs="+")
    args = parser.parse_args()
    expected = (args.runs[0] / "collision.txt").read_bytes()
    setup = (args.runs[0] / "run.txt").read_text()
    for folder in args.runs:
        if (folder / "run.txt").read_text() != setup:
            raise ValueError(f"{folder}: run configuration differs")
        if (folder / "collision.txt").read_bytes() != expected:
            raise ValueError(f"{folder}: collision fingerprint differs")
    print(setup.strip())
    print()
    print(f"Identical collision fingerprints across {len(args.runs)} runs ({len(expected.splitlines())} active colliders).")
    print()
    print("| View | Run | Wall median | Wall p95 | CPU median | Main median | Render median | GPU median | Draws | SetPass | Triangles |")
    print("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|")
    for view in VIEWS:
        for folder in args.runs:
            metrics = profile(folder / f"{view}.csv")
            time_fields = [metrics["wall_ms"][0], metrics["wall_ms"][1]]
            time_fields.extend(metrics[key][0] for key in COLUMNS[1:5])
            fields = [f"{value:.2f}" for value in time_fields]
            fields.extend(f"{metrics[key][0]:.0f}" for key in COLUMNS[5:])
            print("| " + " | ".join((view, folder.name, *fields)) + " |")


if __name__ == "__main__":
    main()
