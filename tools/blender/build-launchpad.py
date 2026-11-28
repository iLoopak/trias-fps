"""Model the TRIAS launch pad. Run with Blender --background --python.

Visual only: the server owns the pad trigger and launch. The pad keeps its 1.85 m footprint and stays
below 8 cm so it never reads as a step. Unity metres, y up, centred on the pad position.
Slots: Ceramic (fired collar), Graphite (well and plinth), Alloy (accelerator coils), Glow (signal gold).
"""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band

art.MATERIALS["Glow"] = ((1.0, .68, .38), 0, .3)


def sector(name, r0, r1, a0, a1, y0, y1, finish, steps=8, bevel=0):
    """A closed annular sector between radii r0..r1 and angles a0..a1 (degrees), from y0 to y1."""
    verts = []
    for y in (y0, y1):
        for r in (r0, r1):
            for i in range(steps + 1):
                a = math.radians(a0 + (a1 - a0) * i / steps)
                verts.append((math.sin(a) * r, y, math.cos(a) * r))
    n = steps + 1

    def v(level, outer, i):
        return level * 2 * n + outer * n + i

    faces = []
    for i in range(steps):
        faces.append((v(1, 0, i), v(1, 1, i), v(1, 1, i + 1), v(1, 0, i + 1)))      # top
        faces.append((v(0, 0, i), v(0, 0, i + 1), v(0, 1, i + 1), v(0, 1, i)))      # bottom
        faces.append((v(0, 1, i), v(0, 1, i + 1), v(1, 1, i + 1), v(1, 1, i)))      # outer wall
        faces.append((v(0, 0, i), v(1, 0, i), v(1, 0, i + 1), v(0, 0, i + 1)))      # inner wall
    faces.append((v(0, 0, 0), v(0, 1, 0), v(1, 1, 0), v(1, 0, 0)))
    faces.append((v(0, 0, steps), v(1, 0, steps), v(1, 1, steps), v(0, 1, steps)))
    art.surface(name, verts, faces, finish, bevel)
    # Wind every face outward regardless of the sector's angular direction.
    import bmesh
    item = art.parts[art.group][-1]
    bm = bmesh.new()
    bm.from_mesh(item.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(item.data)
    bm.free()


art.reset()
art.group = "LaunchPad"
# A low graphite plinth carries six fired-ceramic collar segments held apart by lit gaps.
band("Graphite plinth", (0, 0, 0), [(0, .95, .95, 0), (.03, .93, .93, 0), (.036, .90, .90, 0)], "Graphite", 36, 1.0)
for i in range(6):
    start = i * 60 + 4
    sector("Ceramic collar segment", .70, .90, start, start + 52, .03, .072, "Ceramic", 10, .006)
    gap = i * 60 + 30 + 30
    sector("Collar gap light", .73, .88, gap - 3, gap + 3, .03, .05, "Glow", 2)
# The well is recessed below the collar; six accelerator coils point at the emitter core.
band("Recessed well", (0, 0, 0), [(.02, .70, .70, 0), (.032, .70, .70, 0)], "Graphite", 36, 1.0)
for i in range(6):
    a = math.radians(i * 60 + 30)
    for r, h, w in ((.30, .062, .07), (.46, .055, .075), (.60, .05, .08)):
        sector("Accelerator coil", r - .055, r + .055, i * 60 + 30 - w * 40, i * 60 + 30 + w * 40, .032, h, "Alloy", 3, .004)
band("Emitter core", (0, 0, 0), [(.03, .21, .21, 0), (.05, .19, .19, 0), (.066, .14, .14, 0)], "Glow", 24, 1.0)
band("Core cap", (0, 0, 0), [(.066, .10, .10, 0), (.072, .07, .07, 0)], "Ceramic", 24, 1.0)

art.finish("LaunchPad", "LaunchPad/LaunchPad.fbx", {"LaunchPad": (0, 0, 0)},
           camera_at=(1.4, -1.6, 1.3), camera_target=(0, 0, 0), camera_width=2.3, resolution=(1100, 800))
print("LAUNCHPAD EXPORT", len(art.parts), "mounts")
