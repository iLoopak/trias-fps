---
name: trias-multiplayer
description: Change TRIAS movement rules, server input handling, snapshots or client prediction without breaking parity between shared/rules.js, the server and the Unity Motor.
---

# Multiplayer movement

The server (`match.js`) is authoritative and steps at 30 Hz with `movePlayer` from `shared/rules.js`. The Unity client, the only game client, predicts with `TriasMotor.cs`, a line-for-line port. Read [the kinetic flow reference](../../../KINETIC-FLOW.md) before changing any of them.

Each human command is queued and simulated once, in order (`humanMoves`, `advancePilot`). `ack` is the last simulated command. A late command holds the pilot for at most three ticks and then stays one tick in reserve; held ticks are the only ticks that may take two steps, so command rate never changes speed. Fire, reload, grenade, use and weapon requests are still OR'd when the message arrives. Dead, parked and between-round pilots drain and acknowledge their queue.

Snapshot positions travel to 1 mm. Movement state that changes a later step must be on the wire, or prediction re-bases without it: the jump buffer, dodge buffer and coyote ticks ride in bits 8 to 13 of the player flags (`GRACE_SHIFT`). Add small new player state the same way. Never insert a field into the middle of a row: Unity reads rows by index, and `hordeType` and `frame` are optional trailing values.

The client sends one command per 30 Hz tick of real time: a frame up to one command late sends both, a longer stall re-bases because the server has already bridged it. Arenas without terrain still deserialise an empty `HeightData` (JsonUtility never leaves it null), so movement code reads `ArenaSpec.Terrain`, never `terrain` directly.

A movement rule must be continuous in speed and position. A threshold turns 1 mm of rounding into a different velocity, which the Kinetic Lab shows as corrections even with no jitter.

To change a rule, edit `movePlayer` and `Motor.Step` in the same order, then run `node tools/kinetic-parity/run.mjs` (zero mismatches), `node tools/kinetic-lab.mjs` (network corrections must not rise against `output/kinetic-flow/lab-baseline.json`) and `npm test`. Record authoritative changes in the reference and ask the project owner before making a balance change a default. Restart the local server after changing `match.js` or `rules.js`: the running Node process keeps the old rules.
