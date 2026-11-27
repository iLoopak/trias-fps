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
