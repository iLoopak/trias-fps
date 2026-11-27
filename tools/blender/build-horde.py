"""Model the Horde as malfunctioning station service robots. Run with Blender --background --python.

Each part is authored in the local space of the existing HordeBody joint it mounts to (Unity metres,
y up, z forward), so the procedural gait, wind-up and strike poses stay unchanged. Graphite frames
carry cracked fired-ceramic covers and exposed alloy pistons; a fault glow shows in the fractures and
HordeBody flickers small spark parts at the joints. Slots map in Unity to the per-instance palette:
Shell (ceramic cover), Frame (graphite), Dark, Spark (fault glow), Alloy, Coolant (spitter leak).
"""
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from trias_mesh import band, panel

art.MATERIALS.update({
    "Shell": ((.62, .59, .52), 0, .4), "Frame": ((.05, .055, .06), .2, .45), "Dark": ((.02, .02, .02), 0, .3),
    "Spark": ((1.0, .45, .15), 0, .3), "Alloy": ((.4, .42, .44), .8, .4), "Coolant": ((.45, .7, .25), 0, .3),
})


def tube(name, sections, finish, sides=16, corner=1.0, at=(0, 0, 0)):
    """Loft (height, half width, half depth, forward) sections along y; heights ascending."""
    band(name, at, sorted(sections), finish, sides, corner)


def piston(name, x, y0, y1, z, r):
    tube(name + " rod", [(y0, r * .5, r * .5, 0), (y1, r * .5, r * .5, 0)], "Alloy", 10, at=(x, 0, z))
    tube(name + " cylinder", [(y0, r, r, 0), ((y0 + y1) * .5, r, r, 0)], "Frame", 12, at=(x, 0, z))


art.reset()
art.group = "Torso"
# Graphite chassis with a hunched, forward-carried thorax; the ceramic chest cover is split and one
# plate has slipped, exposing the fault glow and the alloy spine.
tube("Pelvis block", [(-.06, .13, .10, 0), (.06, .14, .105, .01)], "Frame", 16, .55)
tube("Spine column", [(.04, .045, .045, .02), (.30, .05, .05, .045), (.50, .045, .045, .065)], "Alloy", 12)
tube("Thorax frame", [(.20, .15, .10, .035), (.34, .185, .12, .05), (.48, .19, .115, .065), (.58, .12, .08, .075)], "Frame", 18, .6)
panel("Left chest cover", -.075, .42, .19, .13, .20, .03, "Shell", -.12, .008)
panel("Right chest cover (slipped)", .085, .36, .195, .12, .17, .03, "Shell", .28, .008)
panel("Fault glow", .0, .40, .17, .03, .16, .02, "Spark", .1, .002)
panel("Back cover", 0, .42, -.11, .26, .22, .03, "Shell", 0, .01)
panel("Cracked back cover piece", .09, .26, -.105, .10, .10, .025, "Shell", .3, .006)
for side in (-1, 1):
    panel("Shoulder bearing", side * .17, .50, .045, .07, .07, .09, "Alloy", 0, .008)
    for i in range(3):
        panel("Exposed rib strut", side * .10, .25 + i * .05, .13, .09, .012, .02, "Alloy", side * .3, .002)
tube("Hanging cable", [(-.14, .012, .012, 0), (.10, .012, .012, 0)], "Dark", 8, at=(.05, 0, .12))

art.group = "Head"
# Sensor head: an angular graphite casing, half a ceramic faceplate, one live optic and one dead.
tube("Neck actuator", [(-.06, .035, .035, 0), (.03, .03, .03, 0)], "Alloy", 10)
tube("Sensor casing", [(.00, .060, .070, .02), (.08, .080, .090, .025), (.17, .075, .085, .015), (.22, .050, .060, 0)], "Frame", 16, .6)
panel("Half faceplate", -.035, .11, .125, .075, .13, .02, "Shell", -.05, .006)
panel("Broken faceplate edge", .035, .07, .122, .05, .05, .018, "Shell", .4, .004)
panel("Live optic", -.035, .125, .138, .045, .018, .008, "Spark", 0, .002)
panel("Dead optic", .040, .125, .126, .035, .016, .008, "Dark", 0, .002)
panel("Antenna stub", .055, .26, .0, .012, .09, .012, "Alloy", .4, .002)

art.group = "Jaw"
panel("Hanging mandible plate", 0, -.05, .11, .10, .05, .07, "Frame", 0, .006)
panel("Speaker grille", 0, -.045, .118, .07, .02, .01, "Dark", 0, .002)

art.group = "UpperArm"
tube("Upper arm strut", [(-.30, .032, .032, 0), (.02, .036, .036, 0)], "Frame", 12, .7)
piston("Biceps piston", .0, -.26, -.04, .045, .016)
panel("Cracked arm cover", 0, -.12, -.03, .08, .16, .03, "Shell", 0, .006)
tube("Elbow hub", [(-.32, .042, .042, 0), (-.27, .046, .046, 0)], "Alloy", 14)

art.group = "Forearm"
tube("Forearm strut", [(-.26, .028, .028, 0), (.0, .034, .034, 0)], "Frame", 12, .7)
piston("Forearm piston", .0, -.24, -.04, -.035, .013)
tube("Wrist", [(-.30, .030, .030, 0), (-.26, .034, .034, 0)], "Alloy", 12)
panel("Manipulator palm", 0, -.33, .045, .07, .06, .05, "Frame", 0, .006)
for i, x in enumerate((-.025, 0, .025)):
    panel("Claw finger", x, -.39, .055, .014, .08, .016, "Alloy", .15 * (i - 1), .003)

art.group = "Thigh"
tube("Thigh strut", [(-.40, .040, .045, 0), (.03, .055, .060, 0)], "Frame", 14, .6)
piston("Hip piston", .045, -.34, -.04, .05, .018)
panel("Thigh cover", 0, -.16, .09, .09, .18, .03, "Shell", 0, .008)
tube("Knee actuator", [(-.43, .052, .052, 0), (-.37, .056, .056, 0)], "Alloy", 14)

art.group = "Shin"
tube("Shin strut", [(-.40, .034, .040, 0), (.0, .044, .048, 0)], "Frame", 14, .6)
piston("Calf piston", .0, -.36, -.06, -.05, .016)
panel("Shin guard", 0, -.20, .075, .08, .22, .025, "Shell", 0, .008)
panel("Foot block", 0, -.43, .16, .10, .07, .24, "Frame", 0, .012)
panel("Toe plate", 0, -.43, .19, .09, .05, .04, "Shell", 0, .008)

art.group = "CrusherShards"
# Crusher: a demolition unit's oversized ceramic shoulder armour, cracked and riveted.
for side in (-1, 1):
    panel("Heavy shoulder armour", side * .19, .56, .13, .16, .12, .26, "Shell", side * .25, .015)
    panel("Armour crack glow", side * .19, .565, .134, .015, .10, .01, "Spark", side * .2, .002)
panel("Dorsal ballast block", 0, .44, -.11, .24, .24, .08, "Frame", 0, .015)

art.group = "SpitterSac"
# Spitter: a ruptured coolant canister strapped under the sensor head.
tube("Coolant canister", [(-.16, .04, .04, 0), (-.13, .055, .055, 0), (-.04, .055, .055, 0), (-.01, .04, .04, 0)], "Coolant", 16, at=(0, 0, .075))
tube("Canister strap", [(-.10, .060, .060, 0), (-.08, .060, .060, 0)], "Alloy", 16, at=(0, 0, .075))

display = {"Torso": (0, .86, 0), "Head": (.013, 1.44, .155), "Jaw": (.013, 1.46, .195),
           "UpperArm": (-.25, 1.28, 0), "Forearm": (-.31, 1.01, 0), "Thigh": (-.11, .88, 0), "Shin": (-.11, .47, 0),
           "CrusherShards": (.6, .86, 0), "SpitterSac": (.6, 1.44, .155)}
art.finish("Horde", "Horde/Horde.fbx", display, camera_at=(2.6, -4.7, 2.0), camera_target=(0, 0, .9), camera_width=2.4)
print("HORDE EXPORT", len(art.parts), "mounts")
