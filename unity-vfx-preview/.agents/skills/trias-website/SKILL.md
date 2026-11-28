---
name: trias-website
description: Select source-attributed TRIAS gameplay and asset imagery for release pages, website, and store materials.
---

# TRIAS website imagery

Use fresh native gameplay captures for environment claims. Give staged fixed-eye views and Blender studio renders explicit captions when showing them alongside live gameplay. Preserve the distinction between Relay's clean ceramic facilities, Vanta's thermal equipment, Meridian's excavation works and Kordilera's eroded geology.

Keep source path, arena, camera conditions and build or capture date with each selected asset. Use the current approved marketing destination and existing image convention when replacing imagery. Preserve raw playable captures for future crops.

Use Blender renders of the implemented arsenal with an asset-render caption. Use native gameplay captures when describing the first-person experience.

Pilot portraits and arena gallery images come from the art review: `TRIAS_ART_ONLY=portrait-,relay-transfer,vanta-works,site-meridian,kordilera-crossing tools/unity/art-review.sh <out>`. Encode them with `cwebp` (ffmpeg here has no WebP encoder). Caption HUD-free fixed-camera views as "In-engine capture" rather than gameplay. Portraits also reveal model defects that combat distance hides (the floating helmet that led to the gorget), so look at them closely before publishing.
