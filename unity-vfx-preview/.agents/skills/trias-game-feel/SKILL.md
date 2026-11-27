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
