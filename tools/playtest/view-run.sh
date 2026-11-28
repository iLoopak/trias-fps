#!/bin/sh
# Visual stability playtest: joins a bot room with the scripted camera sweep (-trias-sweep) and logs
# per-phase crosshair offset, crosshair jump, eye lead, roll, FOV span and reconciliation corrections.
#   tools/playtest/view-run.sh <player.app> <out-dir> <label> [seconds] [extra player flags...]
# TRIAS_PLAYTEST_PORT is the server port (default 3000). TRIAS_VIEW_DELAY_MS adds a latency proxy
# (tools/playtest/latency-proxy.mjs) with that one-way delay and TRIAS_VIEW_JITTER_MS jitter.
set -e
APP="$1"; OUT="$2"; LABEL="$3"; SECONDS_TO_RUN="${4:-80}"
shift 4 || shift $#
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PORT="${TRIAS_PLAYTEST_PORT:-3000}"
ARENA="${TRIAS_VIEW_ARENA:-relay}"
CODE="VIEW$(echo "$LABEL" | tr -cd '[:alnum:]' | tr '[:lower:]' '[:upper:]')$$"
mkdir -p "$OUT/$LABEL"
TRIAS_SERVER="ws://127.0.0.1:$PORT/play" node "$ROOT/tools/playtest/host-room.mjs" "$CODE" tdm "$ARENA" $((SECONDS_TO_RUN + 30)) > "$OUT/$LABEL/host.log" 2>&1 &
HOST=$!
JOIN_PORT="$PORT"; PROXY=""
if [ -n "$TRIAS_VIEW_DELAY_MS" ]; then
  JOIN_PORT=$((PORT + 101))
  node "$ROOT/tools/playtest/latency-proxy.mjs" "$JOIN_PORT" "ws://127.0.0.1:$PORT/play" "$TRIAS_VIEW_DELAY_MS" "${TRIAS_VIEW_JITTER_MS:-0}" > "$OUT/$LABEL/proxy.log" 2>&1 &
  PROXY=$!
fi
sleep 2
"$APP/Contents/MacOS/TRIAS" -screen-width "${TRIAS_VIEW_WIDTH:-1600}" -screen-height "${TRIAS_VIEW_HEIGHT:-900}" -screen-fullscreen 0 \
  -logFile "$OUT/$LABEL/player.log" -trias-invite "http://127.0.0.1:$JOIN_PORT/join/$CODE" \
  -trias-frame-log "$OUT/$LABEL/frames.log" -trias-sweep -trias-quit-after "$SECONDS_TO_RUN" "$@" > /dev/null 2>&1 || true
kill "$HOST" $PROXY 2> /dev/null || true
grep -c "frames=" "$OUT/$LABEL/frames.log" 2> /dev/null || echo "no frame log"
