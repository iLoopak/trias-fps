---
name: trias-game-director
description: Maintain the pacing and visual hooks of the TRIAS Combat Director, including arena surges.
---

# Game director

Read [W7](../../../W7-DIRECTOR.md) for mode boundaries. Node's director owns pacing and publishes the events that the Unity client displays.
For a presentation pass, keep reactor saturation and brightness local. Relay's star and its direction are stable during a surge. Change the reactor energy and nearby spill through the existing hooks in `TriasWorld.Presentation.cs`.
Check that a new world-space cue communicates only public arena state. Confirm fixed viewpoints in inactive and active states before expanding the cue to other arenas.

Keep static phase architecture independent of combat state. Machined gaps and muted stabilizer surfaces carry their own identity. Preserve the brighter faction and objective feedback for existing public state signals.

Keep the arsenal discharge language distinct from public arena and objective cues. The [weapon identity guide](../../../WEAPON-IDENTITY.md) documents the five report and impact families.
