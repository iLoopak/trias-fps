---
name: trias-blender-pipeline
description: Generate, validate, export, preview, and integrate original TRIAS Blender meshes for the native Unity client.
---

# TRIAS Blender pipeline

Start at [the environment production handbook](../../../ENVIRONMENT-KIT.md) for the executable commands, asset families, import contract, look targets, and budgets. Generators are in the repository's `tools/blender` directory. `trias_mesh.py` provides shared coordinate conversion, bevel and normal handling and FBX setup. Keep the prior two-segment default for hero generators. The environment recipes use one-segment machined facets with weighted normals and omit bevels on registration marks.

Use the catalog collider as the fitting envelope for gameplay cover. Keep the graphite backing readable across gaps in the ceramic plates. Treat aligned, intersecting and intentionally separated shells as authored parts. Keep each source shell closed with outward winding. Avoid placing multiple closed caps at the same top height with overlapping area. Keep each module a single opaque imported visual mesh at a given LOD.

Check the two distinct coordinate steps: modeling uses Unity-style metres via `trias_mesh.coordinates`, and FBX uses the established Blender 180° front-axis correction before export. Confirm front, floor pivot, imported slots and triangle counts after reimport. Bind mounts through `BlenderParts` and map named finishes through the existing world material helpers. Retain authoritative colliders and use the Decor layer on visuals.

Use stable IDs and curated seeds. Keep silhouettes, principal axes, contact heights and main material boundaries at LOD1. A LOD that changes where the surface appears to begin should be discarded or corrected. Treat `Manifest.json` as the generator's output. Keep release provenance and original sources with the project. Put raw renders and review captures in ignored `output/` rather than Resources.

For weapon-specific source files, named assembly mounts and exploded renders, follow [the weapon identity guide](../../../WEAPON-IDENTITY.md).

When art-directing cover from archived gameplay, include the short approach face in the comparison. Use the same orthographic quarter view to compare support jaws and rock shear planes at LOD0, then verify the fitted ends in the native player. Blender’s Python module can run the environment generator and CPU Cycles proofs on a headless Linux host with the repository directory layout preserved.

For hand-held weapons, design a side profile and extrude it with `slab()` in `build-armory.py` rather than stacking octagon `block()`s. Give each weapon its own receiver silhouette: `grip(stock=False)` drops the shared rear block. Write `slab()` and `face()` outlines counter-clockwise, as seen from the side that faces the viewer. Pilot suit parts are authored for adult limb lengths: the rig is corrected at runtime by `PilotProportions`, so do not shrink suit parts to fit the stylised Kenney skeleton.

Horde robots, vehicles and power cells follow the same pattern. A `build-*.py` generator writes an FBX of `_Mount` groups authored in the target joint's or root's local space. Unity binds the FBX with `BlenderParts` and keeps the old primitive build as a fallback. Copy an existing FBX `.meta` (for example Arsenal) so a new model imports with the same settings.

Distant megastructures (`build-megastructures.py`) are authored at their typical size with the arena-facing side on local -z and placed through `TriasWorld.Megastructure()` with a uniform scale. Read silhouettes from the arena: avoid pill-shaped stacked sleeves (they read as rockets) and give a support a visible load (a bearing beam) so it does not read as a free-standing tower. For a weak hero part, render real alternatives side by side before choosing; `explore-wraith.py` is the pattern (one scene, several `art.group`s offset in x, nothing exported). Gameplay props such as the launch pad (`build-launchpad.py`) keep the original footprint and a low height. A `sector()` mesh helper builds segmented rings; recalculate its normals with bmesh so mirrored sectors face outward.
