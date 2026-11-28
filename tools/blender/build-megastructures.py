"""Model the distant TRIAS megastructure silhouettes. Run with Blender --background --python.

These replace box placeholders in the far background of Relay and Vanta. They are decor only (no
collision) and are authored in Unity metres (x right, y up, z forward, root at the base centre) at
their typical size, so Unity scales them uniformly. The faces that turn towards the arena are -z.

Slots map per arena in Unity: Frame (structural graphite/iron), Shell (ceramic or refractory
cladding), Trim (insulation or warm metal), Signal (beacon light), Heat (furnace glow).
"""
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import trias_mesh as art
from mathutils import Matrix, Vector
from trias_mesh import band

art.MATERIALS.update({
    "Frame": ((.17, .2, .24), .2, .55), "Shell": ((.62, .6, .55), .02, .5), "Trim": ((.4, .42, .44), .4, .45),
    "Signal": ((.5, .8, .85), 0, .3), "Heat": ((1.0, .45, .12), 0, .3),
})


def box(name, center, size, finish, yaw=0, pitch=0, roll=0, bevel=0, taper=1.0):
    """A rotated block; taper < 1 narrows the top face in x and z."""
    rot = (Matrix.Rotation(math.radians(yaw), 3, "Y") @ Matrix.Rotation(math.radians(pitch), 3, "X")
           @ Matrix.Rotation(math.radians(roll), 3, "Z"))
    hx, hy, hz = (s * .5 for s in size)
    verts = []
    for y in (-1, 1):
        k = taper if y > 0 else 1.0
        for x, z in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            v = rot @ Vector((x * hx * k, y * hy, z * hz * k))
            verts.append((center[0] + v.x, center[1] + v.y, center[2] + v.z))
    faces = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    art.surface(name, verts, faces, finish, bevel)


def strut(name, a, b, width, depth, finish, bevel=0):
    a, b = Vector(a), Vector(b)
    axis = b - a
    yaw = math.degrees(math.atan2(axis.x, axis.z))
    pitch = -math.degrees(math.atan2(axis.y, math.hypot(axis.x, axis.z)))
    mid = (a + b) * .5
    box(name, mid, (width, depth, axis.length), finish, yaw, pitch, 0, bevel)


def octo(name, at, profile, finish, sides=8):
    """Polygonal loft: (height, half width, half depth, forward offset)."""
    band(name, at, profile, finish, sides, 1.0)


def habitat_pier():
    """Relay: a tapered load column that carries the habitat ring. Three fired-ceramic cladding
    sleeves are held apart by phase gaps and each sits slightly off the axis, a service balcony
    cantilevers towards the arena and a lattice mast carries the beacon. 24 m above the deck."""
    art.group = "HabitatPier"
    octo("Load column", (0, -14, 0), [(0, 2.9, 2.9, 0), (14, 2.5, 2.5, 0), (34, 2.0, 2.0, 0), (36, 2.0, 2.0, 0)],
         "Frame")
    for i, (y0, y1, dx, r) in enumerate(((-4, 4.8, .12, 3.05), (5.6, 13.8, -.16, 2.82), (14.6, 20.4, .2, 2.6))):
        octo("Ceramic sleeve", (dx, 0, 0), [(y0, r * .97, r * .97, 0), (y0 + .15, r, r, 0), (y1, r * .95, r * .95, 0)], "Shell")
        octo("Sleeve seat ring", (0, 0, 0), [(y1 + .05, r * .86, r * .86, 0), (y1 + .35, r * .86, r * .86, 0)], "Trim")
        # Narrow vertical service channels expose the frame through each sleeve on the arena side.
        box("Service channel", (dx, (y0 + y1) * .5, -r * .97), (.55, y1 - y0 - 1.2, .25), "Frame")
    box("Phase gap light", (.05, 5.2, -2.95), (1.6, .14, .12), "Signal")
    for side in (-1, 1):
        strut("Raked buttress", (side * 6.2, -9, .4), (side * 2.4, 6.5, 0), 1.1, 1.6, "Frame")
        strut("Buttress tie", (side * 4.6, -2.8, .3), (side * 2.6, -2.6, .2), .5, .5, "Trim")
    # Cantilevered service balcony with railing on the arena-facing side.
    box("Balcony deck", (0, 16.9, -3.7), (5.2, .35, 2.8), "Trim", bevel=.04)
    box("Balcony brace", (0, 15.6, -2.8), (4.4, .3, .3), "Frame", pitch=-38)
    for x in (-2.4, -1.2, 0, 1.2, 2.4):
        box("Railing post", (x, 17.6, -5.0), (.09, 1.1, .09), "Trim")
    box("Railing", (0, 18.1, -5.0), (5.0, .08, .08), "Trim")
    box("Balcony hatch", (0, 18.0, -2.62), (1.2, 2.0, .2), "Frame")
    # Load saddle: a wide bearing beam that visibly carries the habitat ring above, with raked
    # struts, so the pier reads as structure rather than a free-standing tower.
    box("Saddle seat", (0, 21.6, 0), (4.6, 2.4, 4.6), "Frame", taper=1.25)
    box("Bearing beam", (0, 23.4, 0), (15.5, 1.7, 3.2), "Shell", bevel=.08)
    box("Beam cap", (0, 24.45, 0), (14.5, .4, 2.6), "Trim")
    for side in (-1, 1):
        strut("Saddle strut", (side * 2.2, 18.5, 0), (side * 7.2, 22.6, 0), .7, 1.2, "Frame")
        box("Beam end shoe", (side * 7.4, 24.2, 0), (1.0, 1.4, 3.6), "Frame")
        box("Beam light", (side * 5.2, 23.4, -1.65), (2.4, .16, .1), "Signal")
    for x, z in ((-.6, -.6), (.6, -.6), (.6, .6), (-.6, .6)):
        strut("Mast leg", (x * 1.6 - 3, 24.6, z * 1.6), (x * .5 - 3, 31, z * .5), .2, .2, "Frame")
    box("Mast ring", (-3, 27.6, 0), (1.3, .14, 1.3), "Trim")
    box("Beacon", (-3, 31.3, 0), (.6, .6, .6), "Signal")


def kiln_stack():
    """Vanta: a refractory flue 40 m tall. A buttressed plinth, banded hoops at a structural pitch,
    an external riser, a cage ladder on the arena side and a heat-stained flared crown."""
    art.group = "KilnStack"
    octo("Plinth", (0, 0, 0), [(0, 5.0, 5.0, 0), (3.2, 4.6, 4.6, 0), (3.8, 4.0, 4.0, 0)], "Frame")
    for i in range(4):
        a = i * math.tau / 4 + math.pi / 4
        box("Plinth buttress", (math.sin(a) * 4.3, 2.6, math.cos(a) * 4.3), (1.1, 5.2, 2.6), "Frame",
            yaw=math.degrees(a), pitch=0, taper=.55)
    octo("Flue", (0, 0, 0), [(3.6, 3.3, 3.3, 0), (20, 2.75, 2.75, 0), (35.6, 2.3, 2.3, 0)], "Shell", 12)
    for y in (8, 14, 20, 26, 32):
        r = 3.3 - (y - 3.6) / 32 * 1.0
        octo("Hoop", (0, 0, 0), [(y - .35, r + .2, r + .2, 0), (y + .35, r + .2, r + .2, 0)], "Frame", 12)
    octo("Crown collar", (0, 0, 0), [(35.4, 2.35, 2.35, 0), (36.4, 3.2, 3.2, 0), (38.6, 3.0, 3.0, 0), (39.0, 2.6, 2.6, 0)], "Trim", 12)
    octo("Heat stain", (0, 0, 0), [(39.0, 2.45, 2.45, 0), (40.0, 2.2, 2.2, 0)], "Heat", 12)
    # External riser with brackets, offset to one side to break symmetry.
    octo("Riser pipe", (2.9, 0, 1.6), [(3.8, .55, .55, 0), (31, .55, .55, 0), (32.2, .3, .3, 0)], "Trim", 10)
    for y in (9, 17, 25):
        box("Riser bracket", (2.7, y, 1.45), (1.2, .4, .5), "Frame", yaw=-30)
    # Cage ladder on the arena-facing (-z) side.
    for x in (-.45, .45):
        box("Ladder rail", (x, 19.5, -3.15), (.12, 31, .12), "Frame", pitch=1.5)
    for y in range(6, 34, 4):
        box("Ladder cage hoop", (0, y, -3.4 + (y - 6) * .008), (1.3, .1, .7), "Trim")


def kiln_hall():
    """Vanta: the receiving works. A long kiln hall with a sawtooth roof whose glazed faces glow,
    buttress ribs at a 9.6 m bay, a low annex and a transfer gallery towards the arena."""
    art.group = "KilnHall"
    box("Hall body", (0, 9, 0), (66, 18, 20), "Frame", bevel=.2)
    box("Plinth course", (0, 1.2, -.2), (67.5, 2.4, 20.8), "Trim")
    box("Cladding band", (0, 12.8, -10.05), (64, 4.6, .3), "Shell")
    for i in range(7):
        x = -28.8 + i * 9.6
        box("Buttress rib", (x, 8.6, -10.6), (1.5, 17.2, 1.8), "Frame", taper=.7, bevel=.05)
        box("Rib cap", (x, 17.5, -10.4), (2.0, .7, 2.2), "Shell")
    for i in range(6):
        x = -28.8 + i * 9.6
        # Sawtooth roof: a north-light tooth extruded across the hall depth; its vertical face is glazed.
        art.plate("Sawtooth tooth", [(x, 18, None), (x + 9.6, 18, None), (x + .6, 24.4, None), (x, 24.4, None)], 9.9, 19.8, "Shell", .05)
        art.plate("Clerestory glow", [(x + .05, 18.6, None), (x + .5, 18.6, None), (x + .5, 23.8, None), (x + .05, 23.8, None)], 9.6, 19.2, "Heat", 0)
        box("Tooth ridge", (x + .3, 24.55, 0), (.9, .3, 20.2), "Trim")
    for i in range(5):
        x = -26 + i * 13
        box("Hall door", (x, 4.5, -10.1), (5.2, 7.4, .4), "Trim")
        box("Door lintel", (x, 8.6, -10.3), (6.2, .7, .6), "Shell")
    # Low annex and an inclined transfer gallery reaching towards the arena.
    box("Annex", (-21, 5.5, -16), (22, 11, 12), "Frame", bevel=.1)
    box("Annex roof", (-21, 11.4, -16), (23, .8, 13), "Shell")
    strut("Transfer gallery", (8, 16, -10), (10, 9, -38), 5.0, 4.0, "Shell")
    strut("Gallery underframe", (8, 13.6, -10), (10, 6.6, -38), 3.6, 1.0, "Frame")
    for z, h in ((-20, 13.0), (-30, 10.0)):
        box("Gallery trestle", (9, h * .5, z), (1.4, h, 1.4), "Frame", taper=.8)
    box("Gallery signal", (9.3, 12.7, -30.2), (4.0, .25, .2), "Signal")


art.reset()
habitat_pier()
kiln_stack()
kiln_hall()
art.finish("Megastructures", "Megastructures/Megastructures.fbx", {"HabitatPier": (-40, 0, 0), "KilnStack": (-20, 0, 0), "KilnHall": (28, 0, 0)},
           camera_at=(-30, 110, 34), camera_target=(-5, 0, 16), camera_width=110, resolution=(1600, 800))
print("MEGASTRUCTURES EXPORT", len(art.parts), "mounts")
