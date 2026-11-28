---
name: trias-playtest
description: Produce reproducible screenshots, frame-time samples, and gameplay smoke tests for the TRIAS Unity client.
---

# Playtest

Work from a temporary copy of the Unity project for automated camera fixtures. Build the production scene with `BuildTrias.Open` and keep recorded camera poses and FOV fixed across baseline and edits.
For Relay, check the normal approach, exposed and shaded lanes, the main reactor, Pulse, a pilot at close and combat distances, FX at zero, and actual play with bots. Label staged scenarios separately from live play.
Record active renderers, enabled colliders and frame-time median/p95 using the same sample and display settings. Report the Editor measurements as such. Test `npm test` from the repo root and confirm native builds separately.

For environment production, open `TRIAS > Open environment kit studio` and enter Play Mode for the assembled kit. Use the fixed eye-level capture and frame-sampling probe in `tools/unity` from scratch projects. Compare native render counters, collider fingerprints, and at least one held camera angle per traversal and natural map under review.

For the arsenal, verify primary, alternate and reload against the Game Feel Lab server. Capture resting frames without the HUD, and report blind human recognition separately.

Set `TRIAS_MERIDIAN_REVIEW=1` on the environment fixture to capture the shelf and two HUD-free views. Render the match camera into a temporary target for a clean image and restore its previous target before timing. Record arena load order and background editor activity when comparing runs.

For unattended native play with bots, run the server (`npm start`), build the macOS player and run `tools/playtest/run.sh <TRIAS.app> <out> <arena> [mode] [seconds]`. `host-room.mjs` opens a bot room and keeps an idle host seat. The player joins with `-trias-invite` and writes frame-time percentiles, main-thread time, focus, active renderers and GC memory every 10 s (`-trias-frame-log`), plus a screenshot every 8 s (`-trias-capture`). Set `TRIAS_PLAYTEST_FLAGS=-trias-uncapped` to measure real frame cost without vsync, and `TRIAS_PLAYTEST_NO_CAPTURE=1` when timing, because each screenshot stalls the main thread for about 150 ms. A window that loses focus is throttled by macOS, so check `unfocused=0`. Run arenas in separate, cooled-down sessions: on a fanless M2 a second consecutive run slowed by up to 2×. Add `-trias-autopilot` (in `TRIAS_PLAYTEST_FLAGS`) to make the pilot sprint, weave, jump and dodge through the normal input path. It builds Momentum Chains and puts movement in the measured load, but it does not fire or collect pickups.

Every 10 s the frame log also writes `spikes25` (frames over 25 ms), `spikes_gc` (those that coincide with a collection), `gc_runs`, `local_judder` and `remote_judder`. The two judder values are the mean frame-to-frame change of the shown ground speed relative to the server speed while running, for your own camera and for remote pilots. An `alloc_kb_per_s` line and a `slowest_ms` line per client section follow (`packets`, `parse`, `apply_*`, `world`, `late`, `gui_repaint`, `gui_other`). Judder depends on how packets and input steps fall against frames, so compare variants interleaved and repeated, not one run each. Player flags in `InitDiagnostics` are either value pairs or `Array.IndexOf` switches; a new switch that is added to the pair loop is silently ignored when it is the last argument.

The frame log also writes `gpu_ms` and `render_ms` (median and p95 from `FrameTimingManager`, which needs Frame Timing Stats on in Player Settings), and `spike_causes` with the worst hitch. A hitch is classed as `gc`, `gpu`, `render`, `main` (uncapped runs only) or `present`. Under vsync the main thread waits for the render thread's present, so a vsync frame is never blamed on `main`. Measure real CPU cost uncapped (`-trias-uncapped`) on an otherwise idle machine. Another worktree's Unity build or player skews every number, and native players share `~/Library/Logs/TRIAS/TRIAS/Player.log`, so pass `-logFile <path>` in `TRIAS_PLAYTEST_FLAGS`. `TRIAS_PLAYTEST_PORT` points the runner at a server on another port. A/B flags: `-trias-shadow-distance <m>`, `-trias-shadow-cascades <n>`, `-trias-msaa <n>`, `-trias-no-ao`.
