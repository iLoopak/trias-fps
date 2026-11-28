---
name: trias-level-design
description: Review TRIAS routes, authored cover, arena composition, and visual silhouettes without changing competitive geometry by accident.
---

# Level design

The TRIAS catalog defines authoritative cover, launches and objectives. Unity builds the collider boxes from the same shapes in `TriasWorld.cs`. For visual work, keep these colliders and use Decor-layer meshes for fascia, contacts and outer infrastructure.
In Relay, foreground cover, central reactor, and the orbital collar belong to separate depth layers. Check from multiple 1.58 m eye-level views, including under the gantry. Keep openings and the edges of cover evident when adding a fascia.
Use [the Relay art notes](../../../ART-DIRECTION.md) when adjusting architectural hierarchy. Record geometry changes as a gameplay proposal when they would require shifting collision.

For a fitted Blender cover, compare the authored blocker with the rendered top and corners. Preserve real passage gaps and check the legs of a new frame stay on the original jambs. Keep nature low along paths and place decorative ruins against existing banks. Compare sorted enabled collider transforms and bounds before and after the fit.

At the two outer Kordilera crossings, reserve the full deck width through both ends. For rotated bank hardware, project both horizontal half-extents onto the deck width axis, sample terrain height at the offset foot, and test the actual catalog cover there.

Decor under a walkable slab (ribs, girders, lights) may hang at most about .18 m below the collider, and only where no floor lies within 2.4 m beneath it (`DressRelaySoffit`). Re-dress a building's faces (for example the Kordilera refuge's stone plinth, log courses and turf) rather than adding a cosmetic pitched roof over a walkable flat one. When fitting a kit mesh to cover, reject a mesh whose visible top or sides fall well inside the collider (for example `MachineExtraction` on a 1.56 m terrace, or open frames like `MachineStabilizer`). Kordilera's hill shape is the authoritative heightfield. Surface treatments and low rock slabs could not make its gentle slopes read as rugged, so roughening them is a gameplay decision (movement, sightlines, bot navigation), not an art pass.

Kordilera rocks are drawn from `scenery` and collide through stepped `collider: true` boxes in `cover`, which `rockSteps()` in `shared/rules.js` fits to the measured `ROCK_TOPS` height map of each kit form. When a rock form in `trias_environment_forms.py` changes, re-sample it with `TRIAS_ART_COLLIDERS=1 TRIAS_ART_ROCKS=1 tools/unity/art-review.sh <out> kordilera-hills`, paste the new rows into `ROCK_TOPS`, re-export `World.json` and re-run the collider probe. Anything that reads `cover` for visuals must skip `collider` boxes and draw `scenery` instead.
