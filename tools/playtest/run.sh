#!/bin/sh
# Unattended native playtest: opens a bot room on a running local server (npm start), joins it with
# the native macOS player, logs frame-time percentiles and captures gameplay every 8 s.
#   tools/playtest/run.sh <player.app> <out-dir> <arena> [mode] [seconds]
# Extra player flags go in TRIAS_PLAYTEST_FLAGS (for example "-trias-uncapped -trias-autopilot").
set -e
APP="$1"; OUT="$2"; ARENA="$3"; MODE="${4:-tdm}"; SECONDS_TO_RUN="${5:-80}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CODE="PLAY$(echo "$ARENA" | tr '[:lower:]' '[:upper:]')$$"
mkdir -p "$OUT/$ARENA"
node "$ROOT/tools/playtest/host-room.mjs" "$CODE" "$MODE" "$ARENA" $((SECONDS_TO_RUN + 30)) > "$OUT/host-$ARENA.log" 2>&1 &
HOST=$!
sleep 2
"$APP/Contents/MacOS/TRIAS" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 \
  -trias-invite "http://127.0.0.1:3000/join/$CODE" -trias-frame-log "$OUT/frames-$ARENA.log" \
  ${TRIAS_PLAYTEST_NO_CAPTURE:+-trias-no-capture} -trias-capture "$OUT/$ARENA" -trias-quit-after "$SECONDS_TO_RUN" $TRIAS_PLAYTEST_FLAGS > /dev/null 2>&1 || true
kill "$HOST" 2> /dev/null || true
cat "$OUT/host-$ARENA.log"
cat "$OUT/frames-$ARENA.log" 2> /dev/null || echo "no frame log"
ls "$OUT/$ARENA" | wc -l
