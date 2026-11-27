# TRIAS Signature Arsenal

The five instruments share a material vocabulary and one integration path. Each has a different profile in the player's hand, a distinctive mechanism and a primary and alternate sound generated at runtime. Unity uses the Blender assets in the first-person grip, pilot avatars, pickups, spectator view and Combat Echo. The browser continues to use its existing presentation.

| Weapon | Form and mechanism | Sound and discharge | Authoritative role |
|---|---|---|---|
| Pulse / Heartbeat | Rounded saddle, exposed cylinder and exposed compression struts. Simple piston travel and bottom cassette lock. | Short synthetic stroke with a bright lock click. Triple pulse is one server event, with a clustered audio cue and a stack of three simultaneous rings. | Automatic carbine and triple pulse. |
| Scatter / Hammer | Broad low yoke, two large open pressure chambers and exposed hinge bearings. The paired head drops and opens during reload. | Low pressure body with a distinct metallic tail. Wide pressure cone reads as a short ring. Slug has one heavier trace and a tighter ring. | Close spread and concentrated slug. |
| Lance / Needle | Sparse long rails, suspended focusing annuli and a separate optical tube. The bolt and needle draw backward in line. | Sharp transient and narrow trace. Alternate fire adds a resonance and an alignment ring. Further segments use the endpoints supplied by the server. | Precision shot and limited penetration. |
| Arc / Storm | Vertical transformer stack, asymmetrical open electrodes and an off-axis bus. The cell sled slides sideways. | Short gated electrical sound and alternating prong movement. The alternative preserves the server's horizontal fan endpoints. | Automatic suppression and horizontal fan. |
| Rift / Anomaly | Offset yoke with three independently moving ceramic vanes around a faceted core. The core shifts and the vanes separate during the discharge. | Low pressure and harmonic modulation. Heavy release uses a larger collapse ring and stronger detonation layers. | Explosive projectile and separate heavy projectile. |

## Production

Run the existing Blender generator with Blender 4.5 LTS from the repository root.

```sh
blender --background --factory-startup --threads 6 --python-exit-code 1 \
  --python tools/blender/build-armory.py
blender --background unity-vfx-preview/Art/Arsenal.blend --threads 7 --python-exit-code 1 \
  --python tools/blender/render-armory.py -- "$PWD/output/signature-arsenal/blender"
```

Source geometry is original to TRIAS. The source assembly goes to `Art/Arsenal.blend`, and the game FBX goes to `Assets/Resources/TRIAS/Armory/Arsenal.fbx`. The four named mounts are Receiver, Chamber, Action and Payload. Rift adds Vane1–3 under Chamber. The generator and `BlenderArmory` share the service and chamber pivot contract. Continue to align world muzzle and view-model effects to `PhaseArmory.Muzzle`. Cosmetic animation must remain outside authoritative aim.

`WeaponHandling` owns the equip offsets, weapon-dependent support hands, service sequence and mechanical shot response. `TriasClient.Feel` owns view-model recoil and uses the existing camera motion and recoil controls. `MatchSound` synthesizes original primary and alternate reports, plus short mechanism cues. `RelayEffects` uses its local and spatial source pools and follows server endpoints for tracers and impacts. Existing shield, hit and elimination cues remain on the confirmed-hit path. Environment impact sound remains available with visual effects reduced. Echo uses the same third-person firing mechanism during the first 140 ms of its visual breakup after the server consumes it.

## Review

Run `npm run lab:feel` and use the Unity Game Feel Lab. Keys 1–5 select the weapons, LMB shoots, F alternates, R reloads, F7 refreshes the drill, and F11 hides and restores the HUD. Dodge to create an Echo and use X while it is available. The lab uses the standard simulation and weapon profiles.

Review at gameplay FOV in a shaded and a lit lane. Use the same angle and scale for the unlabelled silhouette plates. Test the mechanical profiles with audio muted, then compare the audio without seeing the weapon. Confirm with actual players before recording a recognition result. The first-person captures, shot/reload sequences, PCM samples, Blender views and logs from this pass are under `output/signature-arsenal/`.

The native Mac run confirmed selection, both shots, exact ammo consumption, a completed reload, a primary hit on a server-controlled target and a shot from an Echo formed by dodging for each weapon. The local verification report records stationary frame timing and audio sample measurements. Keep independent silhouette, muted-motion and audio-only recognition sessions on the playtest list, along with remote silhouettes at combat distances in a full match.

The Rift chamber keeps a 19 mm radial offset at rest. Each ceramic vane opens along the midpoint of its own arc during the existing discharge kick, with a longer throw on alternate fire. The chamber and muzzle retain their existing pivots. Compare the open space around the core from the normal weapon camera with motion at zero and one. The same three vane poses can be studied on the original Arsenal model in the Blender plates under `output/art-direction-pass/`. Native capture of this revision remains pending.
