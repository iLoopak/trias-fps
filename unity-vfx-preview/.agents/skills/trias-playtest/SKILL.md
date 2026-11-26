---
name: trias-playtest
description: Produce reproducible screenshots, frame-time samples, and gameplay smoke tests for the TRIAS Unity client.
---

# Playtest

Work from a temporary copy of the Unity project for automated camera fixtures. Build the production scene with `BuildTrias.Open` and keep recorded camera poses and FOV fixed across baseline and edits.
For Relay, check the normal approach, exposed and shaded lanes, the main reactor, Pulse, a pilot at close and combat distances, FX at zero, and actual play with bots. Label staged scenarios separately from live play.
Record active renderers, enabled colliders and frame-time median/p95 using the same sample and display settings. Report the Editor measurements as such. Test `npm test` from the repo root and confirm native builds separately.
