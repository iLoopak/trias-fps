# TRIAS Architectural DNA

## Local production paths

| Purpose | Location |
|---|---|
| Existing Blender pipeline | `tools/blender/trias_mesh.py`, `build-armory.py`, `build-pilots.py`, `build-vektor.py`, `build-map-props.py`, `build-relay-structure.py` |
| Environment forms and studio automation | `tools/blender/trias_environment_forms.py`, `tools/blender/build-environment.py` |
| Game exports and manifest | `unity-vfx-preview/Assets/Resources/TRIAS/Environment/` |
| Existing Unity fit and new selection | `BlenderParts.cs`, `TriasWorld.EnvironmentKit.cs` and the world partials |
| Diagnostic native review | `tools/unity/environment-review-build.cs`, `tools/unity/environment-review-probe.cs` |
| Local evidence | `output/architectural-dna/` |

The native project uses Unity 6000.3.29f1 with Built-in Forward rendering. The checked working tree had no existing `AGENTS.md` at the project roots. The project-local skill directory contained the established art, level, playtest and knowledge guidance, which the new routing links to. Blender 4.5.17 LTS produced the game exports. The editor project has no connected Pipeline package in its manifest. The review uses the installed Unity editor in batch mode and the local standalone player.

## Generation and review

From the repository root, use the Blender executable on your system. The examples use a shell where `blender` is on PATH.

```sh
blender --background --factory-startup --threads 8 --python-exit-code 1 \
  --python tools/blender/build-environment.py -- \
  --preview "$PWD/output/architectural-dna/blender"

blender --background --factory-startup --threads 8 --python-exit-code 1 \
  --python tools/blender/build-environment.py -- \
  --output "$PWD/output/architectural-dna/source" --save-blend \
  --preview "$PWD/output/architectural-dna/blender" \
  --assets WallPhase,SupportPhase,MachineStabilizer,RockOutcrop,TreeWindward,SiteReclaimedDeflector \
  --views front,side,silhouette,close

python3 tools/blender/environment-contact-sheets.py \
  unity-vfx-preview/Assets/Resources/TRIAS/Environment/Manifest.json \
  output/architectural-dna/blender
```

The contact sheet tool uses Pillow 10.1 or newer. The optional `.blend` contains editable meshes. The versioned Python recipes are the editable sources for component-level changes. Each generator application starts with factory settings, validates both collections and performs a fresh FBX import before replacing the output files. The binary digest covers the files generated on that run. The inventory and geometry data are deterministic for the same Blender version and recipe inputs. FBX headers can contain run-specific export timestamps.

In Unity, select **TRIAS > Open environment kit studio** and enter Play Mode. The floor sample, walls, passage frame, supports, bridge, railing, containers, stabilizer, regional tree, outcrop, undergrowth and ruins use the arena's actual material resolution and lighting. Choose an arena from the camera component's `Arena` field. The capsule has a physical height of 1.58 m.

For before/after comparisons in a live match, copy the two `tools/unity` files into the respective `Assets/Editor/EnvironmentReviewBuild.cs` and `Assets/Scripts/Native/EnvironmentReviewProbe.cs` of two scratch copies. Build each with `-executeMethod Trias.Preview.EnvironmentReviewBuild.Build` and `TRIAS_NATIVE_BUILD` set to its own app path. Start the corresponding Node server on loopback. Launch each native executable with `TRIAS_ENV_REVIEW` pointing to an existing output directory and `TRIAS_ENV_SERVER` pointing to the local WebSocket address. Run comparable trials sequentially. The optional `TRIAS_ENV_FOOTAGE=1` records two short live-match camera passes after benchmark sampling. The probe joins four bot matches, holds eye-height views, saves JPEGs and frame samples, and writes sorted enabled collider fingerprints.

## Form and integration

- **Walls and frames.** The wall grid is 3.2 m by 3.0 m, with continuous pressure backing and chamfered, raked ceramic skins. Standard, recessed, exposed-core, angled, reinforced, phase and end wall modules share boundary dimensions. Openings include a window, return corner, maintenance jambs, and standard, reinforced, machinery and phase frames.
- **Supports and cover.** Tapered bearings, angled and graphite uprights, heavy and phase compression supports and a triangular hanger use a family of flared feet, alloy retainers and separate ceramic shoulders. The cover catalog includes low, full, reinforced, equipment, ceramic, phase and weathered surfaces. Pressure and thermal containers have cradles, sockets and latches.
- **Traversal and machinery.** Walkway, bridge, suspended deck, edge, exposed underside, tread, ramp, floor and swept rail meshes share deck datums and solid contact surfaces. The machine catalog includes phase stabilization, distribution, processing, cooling, maintenance and control equipment, suspension, conduits and extraction.
- **Nature.** Tilted bedding and small interruptions in seam color define the eight geology variants. Primary slopes and shear planes guide the silhouettes. Trees use branched rootstocks with wind-swept angular leaf pads. Bramble, sedge, fern and seed heads use opaque folded geometry. The reclaimed deflector intersects a visible bed with a separated ceramic return and exposed graphite anchorage.
- **Relay Station.** Ceramic pressure huts use varied load panels, a clear existing entrance, seating for the stabilizer and a floor access plate. Fitted pylon and baffle families also appear on the shaft routes. Bridge bays, rails and selected risers preserve the authored walkable surface and use lighter hardware.
- **Vanta Foundry and Meridian Basin.** Vanta uses refractory load cheeks, dark equipment recesses and reinforced quench walls alongside existing molten infrastructure. Meridian uses dusted ceramic, adjustable mechanical cheeks, weathered baffles and fitted survey walls. Existing injection points keep filled cover distinct from the decorative machinery and maintain foundry and excavation landmarks.
- **Kordilera.** Existing terrain triangles, collider logic and height sampling provide the base for the rock and tree shapes. Crossing hardware is expressed as eroded phase deflectors. Turf, silt and slope-conditioned bedding use the existing terrain textures with refined macro transitions. Ground plant clusters remain low and avoid the authored trail.

## Technical contract

- **Scale and orientation.** Author in metres, floor at local zero, Blender Z up on disk and Unity Y up in game. Use the same front correction, FBX units and baked axis conversion as the existing Blender parts loader. Imported meshes have named, applied transforms and one UV channel with dominant-plane projection at .72 tiles per metre.
- **Materials.** Expected slots are Ceramic, Secondary, Graphite, Alloy, Amber, Signal, Refractory, DryStone, DarkStone, OchreStone, Moss, Bark, Leaf, LeafShade and YoungLeaf. Map them to existing Standard families and shared grain, brushed or stone textures. Unity reconstructs appearance from the slots. The manifest records the Blender export order. Unity may reorder imported submeshes and material slots together. Resolve by the imported slot names and verify that each expected slot is present. Signal is localized, muted hardware in the environment. Faction response is owned by existing gameplay materials.
- **Geometry validation.** The generator verifies closed outward manifold source parts, applied position, metric extents, pivot, material indices, finite positions, nondegenerate geometry and UVs, nominal triangle limits and FBX mesh/UV/normal/material round-trip. Authored components can be disconnected. One chamfer segment is enough for large modules in this kit. Tiny registration plates are un-beveled to avoid collapsing edges. Nearly coplanar intersections can turn black in Cycles and need relative geometric offsets.
- **Unity import.** Binary FBX 7400, named `<Key>_Mount` and optional `<Key>_LOD1_Mount`. No animations, cameras, lights or colliders imported. Normals preserved, MikkTSpace tangents from import, secondary UV generation disabled. The authored geometry is opaque and shares runtime materials. Check a selected mesh in Unity after reimport before changing slot names or FBX settings.
- **Game authority.** `FitEnvironment` uses the imported mesh bounds to fit a selected asset while the originally created collider remains enabled. Author enough visibility at high-contact edges. Preserve rotation, height, trajectory and the original bridge and step surfaces. A studio prop has no shared role in live navigation unless the arena catalog independently defines it.
- **LOD.** LOD1 keeps the main forms and removes machining, roots, side branches, small layer changes, and tertiary details. High and low share the same transform in Unity, with .16 relative screen height for architecture and .105 for nature at default LOD bias. Very small tread, edge, floor and ramp assets use one mesh. Step meshes and small vegetation can omit shadow casting. The original solid spans and walls cast a single simplified shadow each so repeated visual submeshes avoid separate shadow submissions. Favor data from the target camera when considering wider propagation.

### Working budgets

These are asset triage ranges for the current native Forward setup. Use the actual manifest counts for import checks and runtime counters for acceptance.

| Asset class | Triangle guide | Opaque slots | Runtime considerations |
|---|---:|---:|---|
| First-person hero equipment | Existing armory budget | Existing armory set | Audit in gameplay FOV and profile separately |
| Feature wall, pylon or machine | 650–1,800 LOD0 | 3–5 | Reuse materials and reserve shadow casting for major forms |
| Cover, railing or traversal bay | 140–1,500 LOD0 | 2–5 | LOD for larger forms, shallow floor details |
| Rock and regional tree | 90–1,100 LOD0 | 2–4 | Shared geometry and simplified branch/crown meshes |
| Small vegetation | 96–450 LOD0 | 2–4 | Opaque and shadowless, low terrain placement |
| Reclaimed feature | Up to 1,450 LOD0 | 7 | Sparse shoreline use |
| Distant megastructure | Existing per-map budget | Existing batched finishes | Verify frame time with the fauna and player effects visible |

## Provenance

The environment geometry and Blender preview finishes were authored procedurally for TRIAS in the three versioned Python files. The kit needs no downloaded models, textures or concept art. Existing Unity material textures retain their own project provenance. The former environment kit uses CC0 model packs documented in [the native README](README.md), with the supplied license files beside the imported assets.

## Review and remaining work

The authored catalog has **64 LOD0 assets** and **56 optional LOD1 meshes**. Blender and Unity independently checked the 120 mounts. The geometry and inventory regenerated from the same recipes with matching per-asset metadata. Import inspection checked the triangle counts, material names, floor pivots and absence of colliders. The architecture FBX contains 47 asset IDs and the nature FBX contains 17.

The first Blender review exposed coplanar caps on the openings, pylons and cover. The revision lowered the core under its cap and offset overlapping cladding. The tree variants now have different branch arrangements. The rock variants have shear-specific inclines and interrupted seams. The reclaimed deflector was brought to the visible edge of the bedding. The revised Blender views and native game captures provide the visual evidence for those changes.

Review evidence appears under `output/architectural-dna/`. The files below are generated and intentionally stay outside version control.

| Evidence | Path below the output directory |
|---|---|
| Labeled Blender contact sheets | `blender/architecture-contact.jpg`, `blender/nature-contact.jpg`, `blender/views-contact.jpg` |
| Editable Blender collections | `source/Architecture.blend`, `source/Nature.blend` |
| Actual Unity studio frame | `after/unity-studio.jpg` |
| Close-range Relay service hut, staged without HUD | `after/relay-hut-entry.jpg`, `after/relay-hut-interior.jpg` |
| Gameplay before and after | `native-comparison.jpg`, with raw JPEGs in `player-before/` and `player-after/` |
| Native walkthrough in bot matches | `gameplay-environment.mp4` |
| Before and after render and collision samples | `performance-table.md` and `performance-repeat-table.md`, with raw CSVs and collider fingerprints in the four player folders |
| Tests and local multiplayer | `regression.log`, `scrimmage.jsonl`, `multiplayer-smoke.log` |
| Native build logs | `player-final-build.log` (macOS), `player-windows-build.log` (Windows x64) |

### Gameplay and performance

The macOS comparison uses Unity 6000.3.29f1, Built-in Forward on Apple M2 and Metal, 1280 × 720, Ultra, uncapped, with 12 active players per lobby including bots. The camera is held at recorded gameplay poses while the match runs normally. Each view records 180 frames. The report discards the first counter sample and computes medians and p95. Bots and the exact combat events vary between trials. Triangles include render passes reported by the Player. Memory is Unity's allocated memory counter.

Both macOS and Windows x64 builds succeeded. Runtime, rendering, and gameplay evidence comes from the macOS Player and the local Node server. The Windows check covers build production.

The reference and optimized builds reported identical enabled collider transforms and bounds for Relay 149, Kordilera 81, Vanta 119 and Meridian 135. The local WebSocket smoke verified a compact and legacy peer in a shared room and observed replicated movement for all four layout revisions. All 105 Node tests passed. The eight scenario bot scrimmage ran through its configured duration or match end.

The first profiling pass identified the largest cost in Relay's shaft and hut views. The revised kit disables per-panel shadow casting for spans and walls and uses one simplified shadow from the original structural body. On the transfer bridge this lowered median draw calls from 2,102 in the earlier kit iteration to 1,689. The original map had 1,494. In the sampled bot matches the original bridge measured 12.00 ms median and the optimized kit measured 15.88 ms median. The hut views exceeded 16.67 ms in the first pair. In the second pair, taken in the opposite order, the optimized bridge measured 7.70 ms compared with a 17.49 ms baseline. The service interior measured 10.01 ms compared with a 20.31 ms baseline. The reversal shows that runtime variance across these short live matches is large. Keep both sets of raw measurements and recheck the Relay CPU margin under controlled repeatable load before treating performance as a release pass.

### Visual acceptance

The representative eye-height Relay values are the service portal and wall construction, low-contrast hardware, physically legible phase gaps and the beam-supported transfer deck. Kordilera reads through open trunks, asymmetrical crowns, inclined separate beds, and the technology partly buried at crossings. The old planar ground heightfield still gives distant slopes a broad silhouette. Some rock faces still read as regular quarry sections, and the thinner edge treatments on the transfer deck need an underside camera pass before further propagation.

The text brief named baseline screenshots. The attachment contained text only. The comparison uses fresh screenshots of the source baseline with matching cameras, alongside the prior Living Megastructures captures already in the workspace for creative context.

The new kit is present in all four native arenas. Propagation beyond the current placements should wait for a wider set of motion and visibility checks, additional profiling on the target graphics tiers, and review of enemy visibility and collider corners on approaches. Concentrate follow-up Relay work on draw submission in the two pressure-hut views and the shaft. In Kordilera, prioritize cliff faces, sparse vegetation clusters in overlooked routes and thinner seams around the foreground paths. In Vanta and Meridian, extend thermal and excavation equipment in selected interiors. The browser version keeps its own presentation path. Release publication and marketing updates are outside this checkpoint.

### Controlled Relay follow-up

The fixed-load check uses a separate macOS Player scene built from production assets. It recreates the Relay shaft layout through `TriasWorld`, uses 12 production pilot rigs and Pulse meshes, and samples the same frame-indexed movement and animation at each camera. Network traffic, bots, input and the gameplay HUD are outside this rendering fixture. Four camera views cover the shaft, the transfer bridge, the hut entrance and the hut interior.

Copy `tools/unity/relay-controlled-probe.cs` and `tools/unity/relay-controlled-build.cs` into scratch projects as `Assets/Scripts/Native/RelayControlledProbe.cs` and `Assets/Editor/RelayControlledBuild.cs`. Build with `-executeMethod Trias.Preview.RelayControlledBuild.Build` and `TRIAS_NATIVE_BUILD` set to a separate macOS app for each source version. Run the players sequentially with `TRIAS_RELAY_PROFILE` pointing to separate output directories. Use `python3 tools/unity/relay-controlled-report.py <run-dir> <run-dir> ...` for the CPU, GPU, draw, SetPass and frame-time tables. The report checks collider equality and confirms that every view produced measurements.

Relay batches adjacent modules within a wall or deck span by their resolved material. Two assembled meshes retain the authored high and low detail geometry. Their combined bounds provide local culling. The original structural bodies retain shadow and collision behavior. The architecture FBX keeps its source meshes CPU-readable for assembly. The generated meshes release their CPU data after upload. Shared LOD selection is now per span, and the snapshot comparison checks its visual effect at all four test cameras.

The raw runs, rendered comparisons and outcome are in `output/relay-controlled/`. This fixture isolates a repeatable rendering workload. It complements the live-match captures and network smoke above.

The before/after fixture found about 1 ms of additional render-thread work inside the hut before batching. The follow-up toggled only the renderers of the eligible runs in one loaded world, in A/B/B/A order at each camera. It saved 48–59 draw calls per view. The transfer bridge and hut interior had lower render-thread medians in both batched blocks. Shaft and hut entrance timings varied across blocks. Independently launched variants retained identical fingerprints for all 149 active Relay colliders. Across the matched screenshot pairs, at most 0.064% of pixels differed by more than 8 of 255 in any channel.

### Relay frame-pacing check with bots

The live pacing probe runs the game scene with a local player and 11 server bots. It holds the shaft and service entrance cameras for 240 frames per block, alternates the separate and batched renderers in one match, and records frame, CPU, GPU and present-wait times. The uncapped diagnostic then freezes the scene and repeats the camera measurements with constant renderer work. For the normal client check, the probe retains the quality tier's VSync and stops after the live blocks.

In a scratch Unity copy, apply `tools/unity/relay-renderer-toggle.patch` from the Unity project root. Copy `tools/unity/relay-jitter-probe.cs` into `Assets/Scripts/Native/RelayJitterProbe.cs` and build with `Trias.Preview.EnvironmentReviewBuild.Build`. Start the Node server on loopback and point `TRIAS_RELAY_JITTER` to the output directory. The probe defaults to port 3087. Use `TRIAS_RELAY_JITTER_MODE=production` to retain VSync. Analyze the CSVs and collider fingerprints with `tools/unity/relay-jitter-report.py`. The renderer-toggle patch and probe belong only in the scratch project.

Compare frozen blocks with the same draw and triangle counts before attributing frame-time variation to the kit. Unity's main-thread present-wait and render-thread timers show where variation accumulates. Keep the GPU timing, focus status and actual VSync configuration alongside those numbers. Unity can aggregate render counters into nearby samples at fixed cadence. Average the counters over the block and retain all valid frame timings.

The frozen Relay follow-up recorded 912 equal-work frames per view. The slow CPU decile still showed increased render-thread and present-wait times, with no increase in GPU median. The live VSync check reproduced a slow service entrance in both renderer variants in one match. Two additional matches with a software pacing request did not reproduce that slowdown and Unity continued to report sync interval 1. Keep the existing batching and production frame-pacing settings. The confirmed result is a presentation and CPU timing signature. The precise host-side trigger remains unverified. See `output/relay-jitter/README.md` and `results.md` for measurements and exclusions.
