"""Model the TRIAS power-up cells (med, shield, surge). Run with Blender --background --python.

Each cell is centred on the pickup's suspended mark (Unity metres, y up). "Glow" maps to the
pickup's catalogue tint in Unity, so colour identity and availability logic are unchanged.
"""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band, panel, plate

art.MATERIALS["Glow"] = ((.6, .9, .7), 0, .3)


def round_band(name, sections, finish, sides=28):
    band(name, (0, 0, 0), sections, finish, sides, 1.0)


def turned(points, angle):
    c, s = math.cos(angle), math.sin(angle)
    return [(x * c + z * s, y, -x * s + z * c) for x, y, z in points]


art.reset()

art.group = "Med"
# A field-medic canister: graphite caps, a split ceramic sleeve and a window onto the glowing medium.
round_band("Lower cap", [(-.27, .110, .110, 0), (-.25, .140, .140, 0), (-.19, .140, .140, 0), (-.17, .126, .126, 0)], "Graphite")
round_band("Upper cap", [(.17, .126, .126, 0), (.19, .140, .140, 0), (.25, .140, .140, 0), (.27, .110, .110, 0)], "Graphite")
round_band("Lower ceramic sleeve", [(-.18, .132, .132, 0), (-.07, .132, .132, 0)], "Ceramic")
round_band("Upper ceramic sleeve", [(.07, .132, .132, 0), (.18, .132, .132, 0)], "Ceramic")
round_band("Medium window", [(-.075, .112, .112, 0), (.075, .112, .112, 0)], "Glow", 24)
for y in (-.072, .072):
    round_band("Window ring", [(y - .008, .142, .142, 0), (y + .008, .142, .142, 0)], "Alloy")
# The medic cross repeats on all four faces so the canister reads from any approach, and four
# alloy clamp ribs tie the caps across the sleeves.
for k in range(4):
    a = k * math.tau / 4
    plate("Cross bar", turned([(-.05, .112, .137), (.05, .112, .137), (.05, .138, .137), (-.05, .112 + .026, .137)], a), 0, .012, "Glow", .002)
    plate("Cross stem", turned([(-.013, .075, .137), (.013, .075, .137), (.013, .175, .137), (-.013, .175, .137)], a), 0, .012, "Glow", .002)
    rib = [(-.012, -.17, .142), (.012, -.17, .142), (.012, .17, .142), (-.012, .17, .142)]
    plate("Clamp rib", turned(rib, a + math.tau / 8), 0, .016, "Alloy", .003)
round_band("Carry handle", [(.27, .04, .04, 0), (.31, .05, .05, 0), (.33, .03, .03, 0)], "Alloy", 16)

art.group = "Shield"
# Three curved ceramic petals around a hexagonal core: the shield folds closed around its source.
# Each petal carries a glowing hexagon (the shield mark) and a broad light seam, and the petals flare
# open at the top so the core shows between them; closed and plain, the cell read as a wall panel.
petal = [(-.13, -.24, .26), (.13, -.24, .26), (.17, .02, .30), (.11, .26, .32), (-.11, .26, .32), (-.17, .02, .30)]
for i in range(3):
    angle = i * math.tau / 3
    plate("Ceramic shield petal", turned(petal, angle), 0, .045, "Ceramic", .008)
    plate("Petal backing", turned([(x * .86, y * .9, z - .045) for x, y, z in petal], angle), 0, .03, "Graphite", .006)
    plate("Petal light seam", turned([(-.022, -.21, .275), (.022, -.21, .275), (.022, -.06, .292), (-.022, -.06, .292)], angle),
          0, .012, "Glow", .002)
    hexagon = [(math.cos(k * math.tau / 6) * .075, .06 + math.sin(k * math.tau / 6) * .075, .312) for k in range(6)]
    plate("Shield mark", turned(hexagon, angle), 0, .014, "Glow", .003)
    plate("Shield mark rim", turned([(x * 1.32, .06 + (y - .06) * 1.32, .305) for x, y, z in hexagon], angle), 0, .01, "Graphite", .003)
band("Hexagonal core", (0, 0, 0), [(-.19, .0, .0, 0), (-.08, .135, .135, 0), (.12, .135, .135, 0), (.22, .0, .0, 0)], "Glow", 6, 1.0)

art.group = "Surge"
# Overcharge: a split crystal held apart by three alloy clamps above a graphite collar.
band("Lower crystal", (0, 0, 0), [(-.24, .0, .0, 0), (-.10, .11, .10, .01), (-.01, .12, .11, 0)], "Glow", 5, 1.0)
band("Upper crystal", (0, .035, 0), [(.0, .115, .105, 0), (.12, .09, .08, -.01), (.26, .0, .0, 0)], "Glow", 5, 1.0)
round_band("Graphite collar", [(-.30, .10, .10, 0), (-.27, .15, .15, 0), (-.23, .15, .15, 0), (-.21, .11, .11, 0)], "Graphite")
for i in range(3):
    clamp = [(-.022, -.24, .15), (.022, -.24, .15), (.022, .16, .17), (-.022, .16, .17)]
    plate("Alloy clamp", turned(clamp, i * math.tau / 3 + .5), 0, .03, "Alloy", .005)
    plate("Ceramic clamp shoe", turned([(-.03, .14, .19), (.03, .14, .19), (.03, .22, .15), (-.03, .22, .15)], i * math.tau / 3 + .5),
          0, .04, "Ceramic", .006)

art.finish("PowerCells", "PowerCells/PowerCells.fbx", {"Med": (-.6, 0, 0), "Shield": (0, 0, 0), "Surge": (.6, 0, 0)},
           camera_at=(1.6, -3.2, 1.0), camera_target=(0, 0, 0), camera_width=2.0, resolution=(1200, 700))
print("POWERCELLS EXPORT", len(art.parts), "mounts")
