#!/bin/sh
# Contact sheet for -trias-burst captures: four consecutive frames around the crosshair, and the average of
# each neighbouring pair (an approximation of what the eye integrates over two 60 Hz refreshes).
#   tools/playtest/burst-sheet.sh <burst-dir> <arena-phase> <out.png> [crop-size]
# Example: tools/playtest/burst-sheet.sh out/a-default-1/burst relay-run sheet.png 360
set -e
DIR="$1"; NAME="$2"; OUT="$3"; SIZE="${4:-360}"
CROP="crop=$SIZE:$SIZE:(iw-$SIZE)/2:(ih-$SIZE)/2"
ffmpeg -loglevel error -y \
  -i "$DIR/$NAME-2.png" -i "$DIR/$NAME-3.png" -i "$DIR/$NAME-4.png" -i "$DIR/$NAME-5.png" \
  -filter_complex "[0]$CROP,split[a][a2];[1]$CROP,split=3[b][b2][b3];[2]$CROP,split=3[c][c2][c3];[3]$CROP,split[d][d2];\
[a2][b2]blend=all_mode=average[ab];[b3][c2]blend=all_mode=average[bc];[c3][d2]blend=all_mode=average[cd];\
[a][b][c][d]hstack=4[top];[ab][bc][cd]hstack=3,pad=iw+$SIZE:ih:$SIZE/2:0[bottom];[top][bottom]vstack" "$OUT"
