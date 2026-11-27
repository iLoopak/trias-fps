#!/bin/sh
# Usage: tools/unity/art-review.sh <output-dir> [arena-or-shot-prefix,...]
# Renders the fixed HUD-free art review shots from the worktree's Unity project.
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
UNITY="${UNITY:-$HOME/Applications/Unity/Hub/Editor/6000.3.29f1/Unity.app/Contents/MacOS/Unity}"
OUT="$(mkdir -p "$1" && cd "$1" && pwd)"
TRIAS_ART_OUT="$OUT" TRIAS_ART_ONLY="${2:-}" "$UNITY" -batchmode -projectPath "$ROOT/unity-vfx-preview" \
  -executeMethod Trias.Preview.ArtReview.Run -logFile "$OUT/unity.log"
grep -E "ART REVIEW COMPLETE|error CS|Exception" "$OUT/unity.log" | head -20
cat "$OUT/review.txt" 2>/dev/null
