"""Summarize matching TRIAS standalone environment benchmark CSVs."""
import argparse
import csv
from math import ceil
from pathlib import Path
from statistics import median

KEYS=("frame_ms","cpu_ms","gpu_ms","draws","triangles","main_ms","allocated_bytes")


def summarize(file):
    with file.open(newline="") as handle:
        rows=list(csv.DictReader(handle))[1:]  # First counter sample may contain the preceding capture frame.
    result={}
    for key in KEYS:
        values=sorted(float(row[key]) for row in rows if float(row[key])>0)
        result[key]=None if not values else (median(values),values[ceil(.95*len(values))-1])
    return result


def pair(before,after,key,precision=2,scale=1):
    a,b=before[key],after[key]
    return "n/a" if a is None or b is None else f"{a[0]/scale:.{precision}f} → {b[0]/scale:.{precision}f}"


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before",type=Path)
    parser.add_argument("after",type=Path)
    args=parser.parse_args()
    print("| View | Frame med ms | Frame p95 ms | CPU med ms | GPU med ms | Draw calls | Triangles | Allocated MiB |")
    print("|---|---:|---:|---:|---:|---:|---:|---:|")
    for file in sorted(args.after.glob("*.csv")):
        baseline=args.before/file.name
        if not baseline.is_file():
            continue
        a,b=summarize(baseline),summarize(file)
        rows=[pair(a,b,"frame_ms"),f"{a['frame_ms'][1]:.2f} → {b['frame_ms'][1]:.2f}",
              pair(a,b,"cpu_ms"),pair(a,b,"gpu_ms"),pair(a,b,"draws",0),
              pair(a,b,"triangles",0),pair(a,b,"allocated_bytes",1,1024**2)]
        print("| "+" | ".join([file.stem,*rows])+" |")
    print()
    for path in sorted(args.after.glob("*-collision.txt")):
        baseline=args.before/path.name
        if baseline.is_file():
            data=path.read_text()
            match=data==baseline.read_text()
            print(f"{path.stem}: {len(data.splitlines())} enabled collider signatures · {'match' if match else 'DIFF'}")


if __name__=="__main__":
    main()
