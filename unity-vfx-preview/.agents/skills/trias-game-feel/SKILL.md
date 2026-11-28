---
name: trias-game-feel
description: Tune TRIAS shooting presentation, view models, pilot poses, and signature effects in Unity.
---

# Game feel

Follow `PhaseArmory.cs` and `WeaponHandling.cs` for Pulse geometry and servicing. The chamber transform and grip anchors connect cosmetic recoil and reload to the existing poses. Test neutral, reload midpoint and return to rest in actual gameplay FOV.
Pulse shares the match camera and environment lighting. Check that the open chamber reads through its geometry in sun and shadow. Match the visual muzzle point when altering the barrel.
Use the existing Compact Avatar rig, and verify faction silhouettes at combat distance as well as close range. Preserve the authoritative simulation and prediction for a presentation change.

Use [the weapon identity guide](../../../WEAPON-IDENTITY.md) for all five instruments. Preserve their pivots and compare their movement and sound with separate capture passes.

Rift’s three ceramic vanes travel along the mid-angle of each modeled arc. When rotating an arc in the Blender recipe, update its chamber-local travel axis in WeaponHandling and inspect the aperture at rest, during both shots and with motion reduced.

Sound effects come from `tools/audio/sfx-manifest.json` through `tools/audio/generate-sfx.mjs` (ElevenLabs). Set `ELEVENLABS_API_KEY` only in the environment and never write it to a file. `RelayEffects.Sfx(id, fallback)` loads `Resources/TRIAS/Audio/Sfx/<id>` and level-matches it to the procedural fallback. To add a sound, add a manifest entry and an `Sfx` call. Check new clips with `Trias.Preview.AudioReview` and spectrograms, and state in the evidence that nobody listened to them.

Per-weapon motion lives in `WeaponHandling.Temperament` (raise time, settle, sway follow, landing weight, bob), and `WeaponHandling.Grip` composes the grip pose for both the client and the art review. Keep raise times at or below the server's 0.24 s fire delay after a switch, and express weight through the settle. Review changes with the `motion-*` art-review shots (frames plus CSV curves), then confirm the feel in the Game Feel Lab.

Snapshots arrive at 30 Hz and prediction steps at 30 Hz, so anything shown at 60 fps or more must move every frame. Remote pilots (`Avatar.Accept`) start each segment at the previous frame's time, not at the packet's arrival. The own camera follows `Stepped()`, a line between the last two prediction steps timed on the send schedule. The server has no lag compensation, so do not add an interpolation buffer for remote pilots. Measure changes with `local_judder` and `remote_judder` in the playtest frame log.

Movement feel follows [the kinetic flow reference](../../../KINETIC-FLOW.md). The own view travels from the last prediction step toward `peek` (the Motor stepped with the keys held this frame), so a press shows in its own frame; reconciliation goes into `correction` and nothing else filters the view. Drive camera and weapon motion from `ShownVelocity` and the step's acceleration, and write new filters as rates in `KineticView` so they look the same at 30 and 240 fps (the parity console prints sway and inertia at four frame rates). Every camera layer scales with **Pohyb kamery**. A dodge that touches down fast gets the skid dip and sound; heavier landings dip by fall speed. Do not add smoothing to hide a correction: find the divergence with `tools/kinetic-lab.mjs` instead.
