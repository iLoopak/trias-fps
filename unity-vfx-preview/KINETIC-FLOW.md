# TRIAS Kinetic Flow

The canonical reference for how TRIAS movement works, why it is tuned the way it is, and how to change it without
breaking prediction. Keep it current after any change to `shared/rules.js` `movePlayer`, `TriasMotor.cs`, the server
input path in `match.js`, or the own-camera path in `TriasClient.cs` / `TriasClient.Feel.cs`.

## Principles

- **Immediate intent.** A key press shows on screen in the frame it happens. The server stays authoritative; the
  client shows the step it is about to send, never a guess the server could not reproduce.
- **One continuous body.** Jump, double jump, dodge, launch and landing hand velocity to each other. No action resets
  speed unless the player asks for it (pulling back, letting the ground brake).
- **Forgiving, never generous.** A press a few milliseconds early or late still counts, but no rule lets a player go
  farther, faster or more often than perfect timing already allowed.
- **Fluidity is not speed.** Ground speeds, jump heights, dodge impulse and cooldowns are unchanged by this pass.
- **Cosmetic stays cosmetic.** Camera and weapon motion never feed aim, hit detection or the Motor.

## Architecture

| Layer | Where | Role |
| --- | --- | --- |
| Rules | `shared/rules.js` `movePlayer` | Authoritative 30 Hz step for human and bot pilots on the server. |
| Native Motor | `TriasMotor.cs` | Line-for-line port of `movePlayer` for Unity prediction. |
| Server input | `match.js` `input`, `humanMoves`, `advancePilot` | Queues each human command and simulates it once, in order. |
| Snapshot | `match.js` `broadcast`, `shared/netcode.js` | Positions to 1 mm; grace ticks in the flag bits. |
| Command clock | `TriasClient.cs` `Update` | 30 Hz; a frame one command late sends both, longer stalls re-base. |
| Own view | `TriasClient.cs` `PeekStep`, `Stepped`, `correction` | Draws the motion between the last step and the next. |
| First-person motion | `TriasClient.Feel.cs`, `KineticView.cs`, `WeaponHandling.cs` | Bob, lean, FOV, sway, inertia, landing. |
| Evidence | `tools/kinetic-lab.mjs`, `tools/kinetic-parity/`, `test/kinetic.test.js` | Measurements, Motor parity, regressions. |

## Tuning (authoritative unless marked)

| Value | Setting | Notes |
| --- | --- | --- |
| Walk / sprint / crouch | 7.55 / 10.1 / 4.25 m/s | Unchanged. Full speed in 4 / 5 ticks. |
| Ground acceleration / stop | 70 / 64 m/s² | Unchanged. Stop from sprint in 5 ticks, 0.64 m. |
| Air acceleration / drag | 20 / 4.2 m/s² | Unchanged values; see air carry. |
| Jump / double jump | 7.65 / 8.75 m/s | Unchanged. Apex 1.41 m, with double 3.28 m. |
| Dodge | 18.3 m/s for 0.22 s, 0.96 s cooldown | Unchanged. |
| `KINETIC_GRACE` | 3 ticks (0.1 s) | **New.** Jump buffer, coyote time, dodge buffer. |
| `AIR_CARRY` | on, ramp 1.5 m/s | **New.** See momentum. |
| Server input reserve | hold ≤ 3 ticks, queue ≤ 8, settle after 60 | **New.** `INPUT_*` in `match.js`. |
| View correction blend | 65 ms time constant, snap above 1.5 m | Cosmetic. Replaced the old 60/s view filter. |

## Transition rules

- **Jump buffer.** A jump press that cannot happen (airborne without the air jump) stays valid for 3 ticks. It can only
  become a ground jump (on landing or within coyote time), never a surprise double jump.
- **Coyote time.** Leaving the ground without an action (walking off a ledge or stairs) allows a ground jump for
  3 ticks. The double jump stays available.
- **Dodge buffer.** A dodge press that cannot happen (cooldown, crouch, air dodge spent) stays valid for 3 ticks and
  fires on the first legal tick. The cooldown is never shortened.
- **Clearing.** A launch pad clears all grace. A jump clears the jump buffer and coyote. A dodge clears all grace.
  Landing clears coyote. Respawn and spawn reset all three.
- **Priority** in one step: launch pad, then jump, then dodge (unchanged). A jump and dodge in the same step is an air
  dodge with jump height and spends the air dodge (unchanged).

| Transition | Behaviour now | Before this pass |
| --- | --- | --- |
| Walk → sprint | Immediate, 5 ticks to 10.1 m/s | Same |
| Sprint → jump | Sprint speed carried, 8.1 m jump | Same |
| Sprint → dodge | 18.3 m/s burst in the input direction | Same |
| Jump → double jump | Any time while airborne | Same |
| Jump/double → land → jump | Press up to 0.1 s before touchdown counts | Press was lost |
| Ledge → jump | Ground jump for 0.1 s, double jump kept | Spent the double jump |
| Dodge → dodge | Press up to 0.1 s before ready counts | Press was lost |
| Air dodge → steer | Speed kept (drag only) while steering | Holding a key braked at 20 m/s² |
| Air → release Shift | Sprint speed kept (drag only) | Braked to walk speed |
| Dodge landing | Brakes to a run in 4 ticks; boots and body respond | Same brake, no feedback |
| Launch pad → double jump | Unchanged | Same |
| Fire / reload / switch while moving | Unchanged; no movement lockouts exist | Same |

## Momentum (air carry)

In the air, input steers the velocity toward the wish direction as before, but when the pilot is faster than the
target speed the resulting speed may not drop faster than letting go of the keys would (4.2 m/s²). Pulling against
the motion still brakes at the full 20 m/s². The rule fades in over the first 1.5 m/s of overspeed so the step stays
continuous; a hard threshold made 1 mm of snapshot rounding change the outcome and multiplied prediction corrections.

The reach envelope is unchanged: holding forward after an air dodge now travels exactly as far as the existing
let-go-of-the-keys technique did (11.14 m from a standing jump), and a ground dodge reaches 6.07 m at touchdown either
way. Before, a player who held a direction (the natural input) lost 1.16 m on an air dodge. Speed never rises above
the speed the pilot already had, so there is no strafe-jump stacking.

**This is an authoritative change that needs project-owner review.** Set `AIR_CARRY = false` in `rules.js` and the
`if (!Grounded && …)` air-carry block in `TriasMotor.cs` together to revert.

## Network invariants

- **Every human command is simulated once, in order.** The server used to apply only the newest command each tick, so
  two commands arriving in one tick became one step and the next tick repeated the old one. Now `humanMoves` takes one
  command per tick. When none has arrived, the pilot holds (at most 3 ticks, only while the stream is live). The late
  command then stays one tick in reserve, so further jitter is absorbed. A reserve untouched for 60 ticks is given back
  with one double step. A pilot never takes more steps than the server has ticked, so sending commands faster does not
  move faster. A backlog above 6 merges its oldest command into the next and keeps its jump and dodge.
- **`ack` is the last simulated command,** not the last received one. Dead, parked and between-round pilots drain
  their queue and acknowledge it. A hold whose command never comes (the client skipped it) is forgiven after a window
  in which the queue ran dry.
- **The client sends one command per 30 Hz tick of real time.** `SendInput` used to re-base its schedule on every late
  frame, so a client rendering below 30 fps sent about 25 commands a second and every missing one became a server
  step it never predicted. A frame up to one command late now sends both; a longer stall still re-bases, because the
  server has already bridged it by repeating the last command (sending the whole backlog measured worse: 46 versus 27
  corrections a minute with hitches).
- **Prediction must see the same holes as the server.** `JsonUtility` gives arenas without terrain an empty
  `HeightData` whose `At()` is 0, so the Motor's `terrain?.At(…) ?? (Shaft ? -∞ : 0)` found a floor at y = 0 everywhere:
  the client never predicted a fall into the Relay shaft or the Vanta channel and the server pulled it down every
  packet (native log: 63–70 corrections per 10 s, up to 14.07 m, until death). `ArenaSpec.Terrain` treats an empty
  grid as no terrain; the parity console now deserialises like Unity and walks off the Relay deck and into the channel.
- **Positions travel to 1 mm.** The snapshot rounded x and z to centimetres, so every packet re-based prediction up to
  7 mm away from the server and flipped wall contacts. `packSnapshot` already rounds to 1 mm, so the server now does too.
- **Grace ticks are state.** Jump buffer, dodge buffer and coyote ride in bits 8 to 13 of the player flags
  (`GRACE_SHIFT` in `netcode.js`, `Motor.Read`). A rule with state that is not on the wire cannot be predicted.
- **Rules must be continuous.** Any new condition on speed or position needs a ramp, not a threshold.
- **Changing a movement rule:** edit `movePlayer` and `Motor.Step` together, then run `node tools/kinetic-parity/run.mjs`
  (zero mismatches required) and `node tools/kinetic-lab.mjs` (network corrections must not rise).

## Own camera and weapon

- **View.** `PeekStep` steps a copy of the Motor with the keys held this frame. The view travels from the last
  predicted step toward that peek on the 30 Hz send schedule. A press is visible in its own frame, and the peek becomes
  the real step when the command is sent, so motion stays continuous. Reconciliation shifts go into `correction`, which
  decays with a 65 ms time constant and snaps above 1.5 m. Nothing else filters the view.
- **Shown velocity.** Lean, bob, FOV and stride read `ShownVelocity` (the same interpolation), so they change every
  frame instead of in 30 Hz stairs.
- **Weapon sway and inertia** use `KineticView`: degrees per second and metres per second squared, scaled to the 60 fps
  per-frame values the temperaments were tuned with. Inertia follows the acceleration the current step applies.
- **Lean** stays at most 1.35°, scaled by **Pohyb kamery**; at 0 the camera has no lean, bob, FOV change or landing dip.
- **Landing tiers.** A fall faster than 4.3 m/s dips by fall speed (up to 12.5 cm) with the landing sound. A dodge that
  touches down faster than 11.6 m/s horizontally gets a short skid dip (up to 4.5 cm) and a quieter landing sound.
  The jump takeoff keeps its small anticipation dip.

## Measured (Kinetic Lab and parity console)

Offline measurements are deterministic: `node tools/kinetic-lab.mjs` on the same code always prints the same numbers.
Baseline is commit `66f0aa8`; the JSON is in `output/kinetic-flow/lab-baseline.json`.

| Network (60 s weave, sprint, jumps, dodges) | Corrections > 10 cm / min, before → after | p99 (cm) | Largest (cm) | Observer uneven ticks / min | Queue |
| --- | --- | --- | --- | --- | --- |
| 20 ms, no jitter | 26 → 0 | 33 → 0.1 | 72 → 3 | 0 → 1 | 0.01 |
| 40 ± 15 ms | 246 → 1 | 37 → 0.1 | 62 → 25 | 0 → 1 | 0.23 |
| 40 ± 35 ms | 519 → 3 | 47 → 0.1 | 126 → 25 | 0 → 1 | 0.46 |
| 80 ± 60 ms | 765 → 4 | 67 → 0.1 | 221 → 21 | 0 → 7 | 0.73 |
| 40 ± 15 ms, client at 25 fps | 297 → 2 | 40 → 0.1 | 61 → 27 | 0 → 5 | 0.63 |
| 40 ± 15 ms, a 50–250 ms frame hitch every ~3 s | 283 → 28 | 41 → 25 | 72 → 61 | 0 → 45 | 0.81 |
| Relay (shaft arena), 40 ± 15 ms | 223 → 6 | 36 → 0.1 | 61 → 34 | 0 → 3 | 0.21 |
| Vanta (channel arena), 40 ± 15 ms | 259 → 4 | 36 → 0.1 | 62 → 34 | 0 → 2 | 0.18 |

"Before" is commit `66f0aa8` with its re-basing client (`KINETIC_LAB_CLIENT=rebase`), "after" this pass. Observer
uneven ticks are server ticks on which other players see this pilot hold or take two steps; the queue column is the
average number of commands waiting, so the reserve adds well under one tick of server-side movement delay. Hitches
remain the main source of corrections: during a stall the server still bridges by repeating the last command.

| Forgiveness | Before | After |
| --- | --- | --- |
| Jump 1–3 ticks before the first possible ground jump | lost | jumps |
| Dodge 1–3 ticks before ready | lost | dodges on the ready tick |
| Jump 0–2 ticks after walking off a ledge | double jump spent | ground jump, double kept |
| 4 ticks early or late | lost / double | lost / double (unchanged) |

| Own camera (model on the real Motor, flat ground) | Before | After |
| --- | --- | --- |
| Keydown → camera moves 1 cm, 60 fps | 33 ms median, 49 worst | 8 ms median, 16 worst |
| Keydown → 10 cm, 60 fps | 67 ms median, 83 worst | 33 ms median, 49 worst |
| Keydown → 1 cm, 144 fps | 32 ms median, 48 worst | 4 ms median, 7 worst |
| Jump → 1 cm rise, 144 fps | 25 ms median, 41 worst | 4 ms median, 7 worst |
| Weapon sway peak, 240°/s turn, 60 / 144 / 240 fps | 1.92 / 0.80 / 0.48 | 1.92 / 1.92 / 1.92 |
| Weapon inertia peak, sprint start, 60 / 144 / 240 fps | 1.19 / 0.53 / 0.33 | 1.05 / 1.06 / 1.05 |

The camera numbers come from the parity console's model of the `TriasClient` view path. They are not an in-engine
capture; the native frame log's `local_judder` and `kinetic` lines are the in-engine check.

### Native player (macOS, M2, Relay TDM with bots, `-trias-autopilot`, 55 s runs, interleaved)

Evidence is in `output/kinetic-flow/` (`final-*`, `ab-*`, `debug-run`, `final-capture` with screenshots). These are
unattended autopilot runs on one machine against a local server, not human play and not a real network.

| Measure (steady 10 s windows) | Baseline `66f0aa8` (4 runs) | This pass (final build, 4 runs) |
| --- | --- | --- |
| Frame time median / p95 | 16.67 / 17.2–17.7 ms | 16.67 / 17.4–17.7 ms |
| Own-view `local_judder` | 7.8–31.8%, mean about 16% | 9.3–25.0%, mean about 16% |
| Remote `remote_judder` | 2.5–14.6%, varies by room | 2.1–19.8%, varies by room |
| Own corrections over 10 cm per window | not logged by that build | 0–3 (largest 39 cm) |

Judder overlaps completely between builds; its run-to-run spread is larger than any difference. The correction
counter found the Relay shaft bug: before the `Terrain` fix, builds of this pass logged 63–70 corrections over 10 cm
per window whenever the autopilot fell into the shaft (each up to 14.07 m). The baseline Motor has the same lookup,
so the baseline shared the bug; it could not count it.

## Rejected and deferred

- **Hard air-carry threshold** (keep full speed whenever faster than the target). Rejected: discontinuous, raised
  corrections from 0 to 29 per minute at 0 ms jitter by turning rounding noise into velocity differences.
- **Server holds without a reserve** (hold, then repay at once). Rejected: perfect local parity but observers saw the
  pilot hold or double-step on 17–43% of ticks under jitter.
- **Bridge debt** (count each repeated step during a stall and drop that many late commands). Rejected: a repeated step
  belongs to no command number, so each one cost two corrections; hitches rose from 28 to 88 a minute.
- **Full client catch-up** (send every missed command after a stall). Rejected: the server has already bridged the
  stall, so the backlog moved the pilot twice (46 corrections a minute with hitches against 27 re-basing).
- **Landing carry** (lower ground friction for a few ticks after an overspeed landing, so a dodge slides). Deferred: it
  lengthens every dodge chain and is a balance change. The skid presentation communicates the brake instead.
- **Double-jump redirect** (turn horizontal velocity toward the input on the second jump). Deferred: new capability,
  needs design review and human testing.
- **Client-side input re-send** (resend a jump until prediction accepts it). Rejected: a prediction miss could become
  a duplicate double jump on the server. Grace lives in the rules instead.
- **Ground acceleration and stop changes.** Not pursued: 4–5 ticks to full speed and 5 to stop already measure as
  snappy; the friction was elsewhere.

## Known limitations

- Subjective feel (is moving enjoyable, is the skid too much, is 0.1 s of grace the right size) needs human
  playtesting. No person has played this pass yet.
- **Trénink offline** (`RelayPilot.Move`, Relay Station scene) is a separate legacy CharacterController model with
  different acceleration and no dodge. It does not use `rules.js` or the Motor and was not changed.
- Kordilera terrain is analytic on the server and a height grid in Unity; the parity console excludes it.
- Network results come from a simulated in-order link in `kinetic-lab.mjs`, not a real WAN.
- Remote pilots under heavy jitter can show the hold and double step described above.
