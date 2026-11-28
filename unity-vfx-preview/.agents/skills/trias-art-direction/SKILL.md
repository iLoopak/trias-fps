---
name: trias-art-direction
description: Art-direct Unity lighting, materials, landmarks, and representative first-person visuals for TRIAS.
---

# Art direction

Read [the TRIAS art direction](../../../ART-DIRECTION.md) and its Relay production notes before material, light, or composition changes.
Relay has a scoped Built-in Forward profile in `TriasWorld.RelayLight.cs`. Keep the star, shadow direction, and planetary illumination on the same vector. Preserve cool fill in shade and a controlled highlight shoulder.
Use the Standard ceramic, graphite and alloy families and authored response maps. Treat large architectural surfaces and small machined components separately to avoid oversized brush texture on gantries.
Evaluate new looks on Relay, Pulse, and Vektor from gameplay cameras before applying them to other arenas or classes.

For close-range environments, use [the Blender kit](../../../ENVIRONMENT-KIT.md). Relay's phase gap reads through aligned retainers and a continuous pressure hull. In Kordilera, use inclined structural bedding, exposed rock faces and outward branching crowns. Review at eye height with the horizon and HUD cropped when possible. Check thin overlapping plates for coplanar shading before adding further finish detail.

For weapons, check the neutral silhouettes in [the weapon identity guide](../../../WEAPON-IDENTITY.md). Evaluate mass and negative space before tuning finish or accent.

For Kordilera, compare the water shelf, wet silt and central current together from the crossing camera. Check both deflector feet against the projected deck edge. The revised water and bank treatment still needs native screenshot approval.

Meridian uses the same polished mineral section on fitted panels and the suspended quarry pages. Keep the north-facing silhouette clear of the ringed planet. Preserve the texture coordinates from before separation when changing an individual page or its motion. Use the optional Meridian views in the native environment probe to check both scales.

Before changing any arena look, render the fixed HUD-free review shots with `tools/unity/art-review.sh <out> [shot-or-arena,...]` (about 11 s per arena) and open the PNGs. `TRIAS_ART_TUNE="ascale=2;seam=.5;stone=1;sat=.5;amb=1.1"` lets you try architecture and ambient variants without code changes. Keep large ceramic fields monolithic (plate-toned seams, ≥7 m period). Keep shade neutral with warm ground bounce, and keep faction colour on helmet, chest and shoulders only. The decisions and rejected variants are in ART-DIRECTION.md, "Vizuální přestavba".

`TRIAS_ART_PICK=2` also reports large distant structures and each object's bounds; use it when a background or ceiling surface is not where the name suggests. `TRIAS_ART_PERF=60` adds the median/p95 CPU time of `Camera.Render` plus draw, SetPass and triangle counts per shot. For a before/after, run it on the branch and on the `main` versions of the world scripts with the same probe, and report it as editor CPU submission time, not GPU or player frame time.

Review gameplay events with the `fx-*` shots: they fire the shipped `RelayEffects` (built through `BuildTrias.CreateEffects`) and capture after `EventAge` seconds, so compare early, mid and late frames. Show what an event means through plate geometry (layers that separate, shells pushed apart, plates converging on a respawn) rather than more particles. Check that tinted fragments stay readable against Relay's pale decks.

For flicker, set `TRIAS_ART_ZFIGHT=1` (renders each shot again 7 mm aside and writes `<shot>-zdiff.png`) and `TRIAS_ART_PIXELS` (lists coplanar surfaces under pixels). For invisible surfaces, `TRIAS_ART_COLLIDERS=1` ray-probes gameplay colliders not covered by a visible mesh. Anomalous mineral that should read as solid stone uses `TRIAS/Mineral Solid` (world-space agate). Floors read as construction through `ArchitectureFinish.Bay` (cast bays, joints and hatches) rather than more seams. Contact skirts and other ground decals must check for floor under their corners so they never hang over a channel or shaft.
