"""Model TRIAS objective hardware. Run with Blender --background --python.

ControlRelay is the mast of a domination or control terminal: slender enough (well under a pilot's
width) that it never reads as cover, tall enough that the hovering phase lens Unity places above it
at 2.35 m identifies the objective across the arena. Unity metres, y up, root at the terminal base.
Slots: Ceramic (fins), Graphite (mast), Alloy (collars and lens cradle).
"""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band, plate


def turned(points, angle):
    c, s = math.cos(angle), math.sin(angle)
    return [(x * c + z * s, y, -x * s + z * c) for x, y, z in points]


art.MATERIALS["Glow"] = ((.4, .9, 1.0), 0, .3)
art.reset()
art.group = "ControlRelay"
band("Mast", (0, 0, 0), [(.12, .055, .055, 0), (1.2, .048, .048, 0), (2.05, .038, .038, 0)], "Graphite", 8, 1.0)
for y in (.95, 1.62):
    band("Mast collar", (0, 0, 0), [(y - .04, .075, .075, 0), (y + .04, .075, .075, 0)], "Alloy", 12, 1.0)
for i in range(3):
    a = i * math.tau / 3
    # Three fired-ceramic fins taper up the mast; a narrow slot keeps each one visibly separated.
    fin = [(-.012, .42, .07), (.012, .42, .07), (.012, 1.88, .09), (-.012, 1.88, .09)]
    plate("Fin root", turned(fin, a), 0, .02, "Graphite", .003)
    outline = [(0, .48, .11), (0, .66, .25), (0, 1.38, .21), (0, 1.84, .13)]
    pts = outline
    # Extrude the fin profile sideways: build both faces explicitly.
    verts = [(-.014, y, z) for _, y, z in pts] + [(.014, y, z) for _, y, z in pts]
    verts = [turned([v], a)[0] for v in verts]
    n = len(pts)
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))] + [(k, (k + 1) % n, (k + 1) % n + n, k + n) for k in range(n)]
    art.surface("Ceramic fin", verts, faces, "Ceramic", .004)
    import bmesh
    item = art.parts[art.group][-1]
    bm = bmesh.new()
    bm.from_mesh(item.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(item.data)
    bm.free()
    prong = [(-.01, 1.98, .05), (.01, 1.98, .05), (.01, 2.24, .14), (-.01, 2.24, .14)]
    plate("Lens cradle prong", turned(prong, a + math.pi / 3), 0, .02, "Alloy", .003)
band("Cradle ring", (0, 0, 0), [(2.02, .07, .07, 0), (2.07, .09, .09, 0)], "Alloy", 12, 1.0)

art.group = "FactionStandard"
# CTF standard: a graphite staff with an alloy grip, a crossbar and a fired-ceramic pennant broken into
# three phase-separated plates (header, faction field, swallowtail). The plates are 6-7 cm thick so the
# standard still reads edge-on. Unity seats the crest on the field at (.35, 1.196, +/-.04).
band("Staff", (0, 0, 0), [(0, .032, .032, 0), (1.70, .026, .026, 0), (1.74, .034, .034, 0)], "Graphite", 10, 1.0)
band("Grip", (0, 0, 0), [(.36, .045, .045, 0), (.56, .045, .045, 0)], "Alloy", 10, 1.0)
band("Pommel", (0, 0, 0), [(-.02, .02, .02, 0), (.04, .05, .05, 0), (.08, .03, .03, 0)], "Alloy", 10, 1.0)
band("Finial", (0, 0, 0), [(1.74, .045, .045, 0), (1.80, .03, .03, 0), (1.90, .0, .0, 0)], "Ceramic", 8, 1.0)
art.plate("Crossbar", [(0, 1.585, None), (.70, 1.585, None), (.72, 1.615, None), (0, 1.615, None)], .02, .04, "Alloy", .004)
art.plate("Ceramic header", [(.04, 1.43, None), (.66, 1.43, None), (.70, 1.57, None), (.04, 1.57, None)], .035, .07, "Ceramic", .008)
art.plate("Header light", [(.06, 1.41, None), (.64, 1.41, None), (.64, 1.425, None), (.06, 1.425, None)], .038, .076, "Glow", .001)
art.plate("Faction field", [(.04, 1.02, None), (.62, 1.04, None), (.64, 1.39, None), (.04, 1.39, None)], .03, .06, "Faction", .008)
art.plate("Swallowtail", [(.04, .80, None), (.30, .90, None), (.58, .78, None), (.60, .99, None), (.04, .99, None)], .03, .06, "Faction", .008)
art.plate("Tail edge", [(.04, .99, None), (.60, .99, None), (.60, 1.005, None), (.04, 1.005, None)], .033, .066, "Graphite", .001)

art.finish("Objectives", "Objectives/Objectives.fbx", {"ControlRelay": (0, 0, 0), "FactionStandard": (1.2, 0, 0)},
           camera_at=(2.6, -3.4, 1.6), camera_target=(.6, 0, 1.2), camera_width=3.4, resolution=(1000, 1000))
print("OBJECTIVES EXPORT", len(art.parts), "mounts")
